#!/usr/bin/env python3
"""
Product image batch processor (throwaway CLI).

Pipeline per image:
  1. Load          - EXIF orientation applied, alpha composited onto white.
  2. Detect        - bounding box found on a THROWAWAY preprocessed copy
                     (CLAHE -> histogram clip -> denoise -> Canny -> morph).
                     The preprocessing never reaches the output.
  3. Crop          - original pixels only: product box + margin, extended to a
                     square using real pixels from the original wherever they exist.
  4. Background    - if the original ran out of pixels, the BACKGROUND is expanded
                     to fill the square canvas. The product band is never scaled.
                       expansion <= threshold (default 26%) -> nine-slice stretch
                       expansion >  threshold               -> seam insertion
                                                               (product protected)
  5. Resize        - side < min-size  -> min-size x min-size   (default 800)
                     side > max-size  -> max-size x max-size   (default 2000)
                     otherwise left at native size.
  6. Save          - JPEG with an embedded sRGB ICC profile.

Usage:
    python process_images.py ~/Desktop/my_images
    python process_images.py ~/Desktop/my_images --output-folder ~/Desktop/out --debug
"""

import argparse
import collections
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageCms, ImageOps

SUPPORTED_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"}


# ----------------------------------------------------------------------------
# 1. Loading
# ----------------------------------------------------------------------------

def load_image(path):
    """Return (bgr_uint8, alpha_or_None). EXIF rotation applied, alpha kept."""
    with Image.open(path) as opened:
        oriented = ImageOps.exif_transpose(opened)

        alpha = None
        if oriented.mode in ("RGBA", "LA") or "transparency" in oriented.info:
            rgba = oriented.convert("RGBA")
            alpha = np.array(rgba.getchannel("A"))
            white = Image.new("RGBA", rgba.size, (255, 255, 255, 255))
            rgb = Image.alpha_composite(white, rgba).convert("RGB")
        else:
            rgb = oriented.convert("RGB")

        bgr = cv2.cvtColor(np.array(rgb), cv2.COLOR_RGB2BGR)

    return bgr, alpha


# ----------------------------------------------------------------------------
# 2. Detection (throwaway preprocessing)
# ----------------------------------------------------------------------------

ANALYSIS_LIMIT = 2400       # detection resolution cap; fabric weave disappears if this is low
TEXTURE_WINDOW = 7          # local-standard-deviation window used as the texture signal
TEXTURE_DETAIL_SIGMA = 4    # anything blurrier than this is not surface texture
SWEEP_TEXTURE_LIMIT = 2.0   # border texture above this means the frame is filled with product,
                            # not a sweep: measured 0.00-0.64 on studio sweeps, 2.4-10.6 on
                            # detail shots where fabric runs to the frame edge
OUTLIER_SPREAD_MULTIPLIER = 4.0  # robust-spread multiples above background that count as product
MIN_COMPONENT_AREA_FRACTION = 0.0005  # min blob size, as a fraction of image area
MIN_COMPONENT_AREA_RATIO = 0.05       # min blob size, as a fraction of the largest blob
MIN_COMPONENT_AREA_PIXELS = 25.0      # min blob size, as an absolute pixel floor
WHOLE_FRAME_FRACTION = 0.985     # box covering this much of the frame counts as "no detection"
LOW_BACKGROUND_FRACTION = 0.06   # below this much background, pad instead of stretch/seam
CENTER_PRIOR_FALLOFF = 0.8       # radius (as a fraction of half-width) of the center-prior bias
SHADOW_EDGE_KERNEL = 15    # opening size that strips a hard shadow's thin edge from
                            # texture-only pixels; measured on Oysho product shots
CANNY_SIGMA = 0.33         # auto-Canny threshold width around the median gradient
                            # (standard heuristic - avoids per-image manual tuning)
CANNY_CLOSE_KERNEL = 5     # gap-closing size applied to the Canny edge map before
                            # flood-filling background in from the frame border


def build_analysis_layers(bgr):
    """Return (chroma_a, chroma_b, texture) for detection. Never touches the output.

    CLAHE is applied to the lightness channel purely to expose texture: on a white
    garment against a white sweep the global contrast is a couple of Lab units, but
    the weave still carries local structure that local equalisation lifts clear of
    the noise floor. The enhanced copy is thrown away once the box is known.
    """
    denoised = cv2.bilateralFilter(bgr, 5, 40, 40)
    lab = cv2.cvtColor(denoised, cv2.COLOR_BGR2LAB)
    lightness, a_channel, b_channel = cv2.split(lab)

    equalized = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8)).apply(lightness)
    equalized = equalized.astype(np.float32)

    # High-pass before measuring texture. CLAHE amplifies every local contrast it
    # finds, and the penumbra of a cast shadow is a local contrast - a slow ramp
    # gets stretched into something that reads as structure. Subtracting a blurred
    # copy discards anything that varies slowly, leaving weave, knit and edges.
    # On the beanie shot this took fabric-versus-shadow separation from 8x to 26x.
    detail = equalized - cv2.GaussianBlur(equalized, (0, 0), TEXTURE_DETAIL_SIGMA)

    mean = cv2.boxFilter(detail, -1, (TEXTURE_WINDOW, TEXTURE_WINDOW))
    mean_of_squares = cv2.boxFilter(detail * detail, -1, (TEXTURE_WINDOW, TEXTURE_WINDOW))
    texture = np.sqrt(np.maximum(mean_of_squares - mean * mean, 0.0))

    return a_channel.astype(np.float32) - 128.0, b_channel.astype(np.float32) - 128.0, texture


def border_ring(shape, fraction=0.02):
    """Boolean mask of a thin frame around the edge - assumed to be background."""
    height, width = shape[:2]
    band_y = max(2, int(height * fraction))
    band_x = max(2, int(width * fraction))
    ring = np.zeros((height, width), dtype=bool)
    ring[:band_y] = ring[-band_y:] = True
    ring[:, :band_x] = ring[:, -band_x:] = True
    return ring


def robust_spread(values):
    """Median absolute deviation, scaled to compare with a standard deviation."""
    return 1.4826 * float(np.median(np.abs(values - np.median(values))))


def fit_background_plane(channel, sample):
    """Fit channel ~ c0 + c1*x + c2*y over sample pixels, evaluated at every pixel.

    A single global median (the old approach) assumes the background is one flat
    colour everywhere. It usually isn't: a backdrop curving into a floor, or a
    soft lighting falloff, drifts by a few Lab units across the frame - small,
    but enough to cross the same threshold that's supposed to mean 'product'.
    Fitting a plane instead of a constant absorbs that gradual drift into the
    expected background at each position, so only a sharp, local deviation (the
    product) still registers as different.
    """
    height, width = channel.shape
    ys, xs = np.mgrid[0:height, 0:width]
    sample_x, sample_y = xs[sample], ys[sample]
    values = channel[sample]

    if values.size < 500:
        return np.full((height, width), float(np.median(values)), dtype=np.float32)

    x_norm = (sample_x - width / 2.0) / (width / 2.0)
    y_norm = (sample_y - height / 2.0) / (height / 2.0)
    design = np.column_stack([np.ones_like(x_norm), x_norm, y_norm])
    coeffs, *_ = np.linalg.lstsq(design, values, rcond=None)

    full_x = (np.arange(width) - width / 2.0) / (width / 2.0)
    full_y = (np.arange(height) - height / 2.0) / (height / 2.0)
    grid_x, grid_y = np.meshgrid(full_x, full_y)
    return (coeffs[0] + coeffs[1] * grid_x + coeffs[2] * grid_y).astype(np.float32)


def canny_enclosed_region(bgr, canny_sigma, close_kernel):
    """Pixels an edge boundary walls off from the frame border - candidate product.

    No background colour model at all: flood the 'free' (non-edge) space from the
    frame border (known background) through 8-connectivity. Whatever the flood
    can't reach - because the product's own silhouette blocks it - is returned,
    including the boundary edge itself. This is corroborating evidence only (see
    build_foreground_mask): a single weak point anywhere on a product's boundary
    lets the flood leak straight through and the whole product reads as
    background, so this must never be trusted as the sole authority - only to
    extend a detection the chroma/texture signals already made independently.
    """
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    denoised = cv2.bilateralFilter(gray, 5, 40, 40)
    median = float(np.median(denoised))
    low = int(max(0, (1.0 - canny_sigma) * median))
    high = int(min(255, (1.0 + canny_sigma) * median))
    edges = cv2.Canny(denoised, low, high)
    edges = cv2.morphologyEx(edges, cv2.MORPH_CLOSE,
                             cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (close_kernel, close_kernel)))

    free = (edges == 0).astype(np.uint8)
    _, labels = cv2.connectedComponents(free, connectivity=8)
    border_labels = set(np.unique(labels[0, :])) | set(np.unique(labels[-1, :])) \
                   | set(np.unique(labels[:, 0])) | set(np.unique(labels[:, -1]))
    border_labels.discard(0)

    background = np.isin(labels, list(border_labels))
    return ~background


def build_foreground_mask(bgr, chroma_floor, texture_floor, shadow_edge_kernel,
                          canny_sigma=CANNY_SIGMA, canny_close_kernel=CANNY_CLOSE_KERNEL):
    """Product = differs in colour from the sweep, or carries surface texture.

    Lightness is deliberately NOT a criterion. A cast shadow is an almost pure
    lightness change - same hue as the sweep, no texture - so any rule that keys
    on 'darker than the background' swallows the shadow along with the product.
    Measured on a beanie shot, the shadow ran to -45 L, far past anything a
    darkness threshold could separate, while its chroma and texture stayed at
    background level.

    A *hard*-edged shadow is different: its crisp boundary is itself local
    high-frequency detail, so it survives the same high-pass filter that's
    meant to isolate fabric weave and lights up the texture signal right along
    the shadow's silhouette - with no chroma difference from the sweep, since
    a shadow has no colour of its own. The two are told apart by shape, not
    intensity: real texture-only detection (e.g. white-on-white weave) fills a
    solid 2D area, while a shadow's edge is a thin line sprawled over a large
    bounding box. Eroding away texture-only, chroma-unsupported pixels that
    don't survive a small open keeps the former and strips the latter.

    A background that isn't perfectly flat - a backdrop curving into a floor, a
    lighting falloff - is handled by fitting the expected background colour as a
    plane across the frame instead of one constant (see fit_background_plane),
    fit only over the border ring: every side of the frame is sampled, so both
    a wall-toned and a floor-toned region are represented if both exist, and
    unlike broadening the sample to wherever a Canny edge map thinks is
    background, the ring can never be contaminated by product pixels.

    Canny + border-flood-fill (see canny_enclosed_region) adds one more signal,
    but only as corroboration: an enclosed region is folded in solely where it
    touches a pixel the chroma/texture signals already flagged, so it can extend
    or fill gaps in a real detection (useful on strongly textured surfaces) but
    can never introduce a wholly separate region on its own - which is how an
    isolated shadow silhouette, or a stray edge in the sweep, could otherwise
    sneak in.
    """
    chroma_a, chroma_b, texture = build_analysis_layers(bgr)
    ring = border_ring(bgr.shape)

    background_a = fit_background_plane(chroma_a, ring)
    background_b = fit_background_plane(chroma_b, ring)
    chroma_distance = np.hypot(chroma_a - background_a, chroma_b - background_b)

    chroma_limit = max(chroma_floor, OUTLIER_SPREAD_MULTIPLIER * robust_spread(chroma_distance[ring]))
    texture_limit = max(texture_floor, float(np.median(texture[ring]))
                        + OUTLIER_SPREAD_MULTIPLIER * robust_spread(texture[ring]))

    chroma_mask = chroma_distance > chroma_limit
    texture_mask = texture > texture_limit
    texture_only = (texture_mask & ~chroma_mask).astype(np.uint8) * 255
    texture_only = cv2.morphologyEx(texture_only, cv2.MORPH_OPEN, cv2.getStructuringElement(
        cv2.MORPH_ELLIPSE, (shadow_edge_kernel, shadow_edge_kernel)))
    mask = (chroma_mask.astype(np.uint8) * 255) | texture_only

    enclosed = canny_enclosed_region(bgr, canny_sigma, canny_close_kernel)
    _, enclosed_labels = cv2.connectedComponents(enclosed.astype(np.uint8), connectivity=8)
    touching_labels = np.unique(enclosed_labels[mask > 0])
    touching_labels = touching_labels[touching_labels != 0]
    corroborated = np.isin(enclosed_labels, touching_labels)
    mask = mask | (corroborated.astype(np.uint8) * 255)

    # Kill speckle, then bridge the gaps between separately-detected parts
    # (a print, a sleeve, the shaded side of a fold) into one region.
    height, width = mask.shape
    bridge = max(9, (int(0.02 * min(height, width)) | 1))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,
                            cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)), iterations=1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE,
                            cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (bridge, bridge)), iterations=2)
    return mask, float(np.median(texture[ring]))


def union_of_significant_components(mask):
    """Bounding box over every blob big enough to belong to the product."""
    height, width = mask.shape[:2]
    count, _, stats, _ = cv2.connectedComponentsWithStats(mask, connectivity=8)
    if count <= 1:
        return None

    largest = stats[1:, cv2.CC_STAT_AREA].max()
    threshold = max(MIN_COMPONENT_AREA_FRACTION * width * height,
                    MIN_COMPONENT_AREA_RATIO * largest, MIN_COMPONENT_AREA_PIXELS)
    boxes = [stats[i] for i in range(1, count) if stats[i, cv2.CC_STAT_AREA] >= threshold]
    if not boxes:
        return None

    x0 = min(int(b[cv2.CC_STAT_LEFT]) for b in boxes)
    y0 = min(int(b[cv2.CC_STAT_TOP]) for b in boxes)
    x1 = max(int(b[cv2.CC_STAT_LEFT] + b[cv2.CC_STAT_WIDTH]) for b in boxes)
    y1 = max(int(b[cv2.CC_STAT_TOP] + b[cv2.CC_STAT_HEIGHT]) for b in boxes)
    return x0, y0, x1 - x0, y1 - y0


Detection = collections.namedtuple("Detection", "box mask ring_texture canvas_contacts")


def detect_product(bgr, alpha=None, chroma_floor=2.0, texture_floor=2.0,
                   analysis_limit=ANALYSIS_LIMIT, bleed_contact=0.2,
                   shadow_edge_kernel=SHADOW_EDGE_KERNEL,
                   canny_sigma=CANNY_SIGMA, canny_close_kernel=CANNY_CLOSE_KERNEL):
    """Locate the product and report how trustworthy that location is."""
    height, width = bgr.shape[:2]
    full_frame = (0, 0, width, height)

    # A real alpha channel beats any heuristic: the box comes straight from the
    # transparency (already flattened onto white as plain 8-bit sRGB by
    # load_image), and the mask/edge-contact check below is the same one every
    # other image goes through, so bleed-off-canvas detection still works.
    if alpha is not None and alpha.min() < 250:
        columns = np.where(alpha.max(axis=0) > 8)[0]
        rows = np.where(alpha.max(axis=1) > 8)[0]
        if columns.size and rows.size:
            box = (int(columns[0]), int(rows[0]),
                   int(columns[-1] - columns[0] + 1), int(rows[-1] - rows[0] + 1))
            mask = (alpha > 8).astype(np.uint8) * 255
            contacts = count_canvas_contacts(mask, bleed_contact)
            # If the opaque region is basically the whole frame, the alpha channel
            # told us nothing either - same collapse the heuristic path applies below.
            if box[2] * box[3] >= WHOLE_FRAME_FRACTION * width * height:
                box = full_frame
            return Detection(box, mask, 0.0, contacts)

    scale = min(1.0, analysis_limit / float(max(height, width)))
    small = (cv2.resize(bgr, (max(8, int(width * scale)), max(8, int(height * scale))),
                        interpolation=cv2.INTER_AREA) if scale < 1.0 else bgr)

    mask, ring_texture = build_foreground_mask(small, chroma_floor, texture_floor, shadow_edge_kernel,
                                               canny_sigma, canny_close_kernel)
    box = union_of_significant_components(mask)

    contacts = count_canvas_contacts(mask, bleed_contact)
    if box is None:
        return Detection(full_frame, mask, ring_texture, contacts)

    if scale < 1.0:
        x, y, w, h = box
        x = max(0, int(np.floor(x / scale)) - 1)
        y = max(0, int(np.floor(y / scale)) - 1)
        w = min(width - x, int(np.ceil(w / scale)) + 2)
        h = min(height - y, int(np.ceil(h / scale)) + 2)
        box = (x, y, w, h)

    # If "the product" is basically the whole frame, detection told us nothing.
    if box[2] * box[3] >= WHOLE_FRAME_FRACTION * width * height:
        box = full_frame
    return Detection(box, mask, ring_texture, contacts)


# ----------------------------------------------------------------------------
# 3a. Detail shots: the product bleeds off the canvas
# ----------------------------------------------------------------------------

def count_canvas_contacts(mask, min_contact):
    """How many canvas edges the product runs off, ignoring incidental touches."""
    if mask is None:
        return 0
    borders = (mask[0, :], mask[-1, :], mask[:, 0], mask[:, -1])
    return sum(1 for border in borders if float((border > 0).mean()) >= min_contact)


def compute_saliency(bgr, working_size=192):
    """Spectral-residual saliency (Hou & Zhang). Places a crop window, nothing else.

    The Fourier log-amplitude of a natural image is close to a smooth 1/f curve;
    whatever departs from it is the unusual part of the picture. Subtracting the
    smoothed log-amplitude and transforming back leaves those departures - seams,
    stitching, a drawstring - and leaves flat sweep and flat fabric near zero.
    """
    height, width = bgr.shape[:2]
    scale = working_size / float(max(height, width))
    small = cv2.resize(bgr, (max(16, int(width * scale)), max(16, int(height * scale))),
                       interpolation=cv2.INTER_AREA)
    gray = cv2.cvtColor(small, cv2.COLOR_BGR2GRAY).astype(np.float32)

    spectrum = np.fft.fft2(gray)
    log_amplitude = np.log(np.abs(spectrum) + 1e-8)
    residual = log_amplitude - cv2.blur(log_amplitude, (3, 3))
    reconstructed = np.abs(np.fft.ifft2(np.exp(residual + 1j * np.angle(spectrum)))) ** 2

    saliency = cv2.GaussianBlur(reconstructed, (0, 0), 3.0)
    saliency = cv2.normalize(saliency, None, 0.0, 1.0, cv2.NORM_MINMAX)
    return cv2.resize(saliency, (width, height), interpolation=cv2.INTER_LINEAR)


def apply_center_prior(saliency, strength=0.35):
    """Mild pull towards the middle - studio framing is deliberate, so ties go centre."""
    height, width = saliency.shape
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    distance = ((xs - width / 2.0) / (width / 2.0)) ** 2 + ((ys - height / 2.0) / (height / 2.0)) ** 2
    prior = np.exp(-distance / (2 * CENTER_PRIOR_FALLOFF ** 2))
    return saliency * ((1.0 - strength) + strength * prior)


def most_salient_square(bgr):
    """Largest square that fits inside the frame, placed over the busiest content."""
    height, width = bgr.shape[:2]
    side = min(height, width)
    if height == width:
        return 0, 0, side, side

    saliency = apply_center_prior(compute_saliency(bgr))
    integral = cv2.integral(saliency.astype(np.float64))
    horizontal = width > side
    limit = (width - side) if horizontal else (height - side)
    step = max(1, limit // 400)

    # The stride skips positions to stay fast, which can skip the flush-to-edge
    # position (limit) itself when limit isn't a multiple of step - the set union
    # guarantees it's tested exactly once regardless.
    best_offset, best_score = 0, -1.0
    for offset in sorted(set(range(0, limit + 1, step)) | {limit}):
        x0, y0 = (offset, 0) if horizontal else (0, offset)
        total = (integral[y0 + side, x0 + side] - integral[y0, x0 + side]
                 - integral[y0 + side, x0] + integral[y0, x0])
        if total > best_score:
            best_score, best_offset = total, offset

    return (best_offset, 0, side, side) if horizontal else (0, best_offset, side, side)


# ----------------------------------------------------------------------------
# 3. Cropping with margin, extended to a square using real pixels
# ----------------------------------------------------------------------------

def compute_margin(box, margin_percent):
    """One margin value for both axes so the framing looks even."""
    return int(round(max(box[2], box[3]) * margin_percent / 100.0))


def compute_square_crop(image_shape, box, margin):
    """Ideal square around box+margin, clipped to what the original actually has."""
    height, width = image_shape[:2]

    desired_x0 = box[0] - margin
    desired_y0 = box[1] - margin
    desired_x1 = box[0] + box[2] + margin
    desired_y1 = box[1] + box[3] + margin

    side = max(desired_x1 - desired_x0, desired_y1 - desired_y0)
    center_x = (desired_x0 + desired_x1) / 2.0
    center_y = (desired_y0 + desired_y1) / 2.0

    square_x0 = int(round(center_x - side / 2.0))
    square_y0 = int(round(center_y - side / 2.0))

    # Slide the square back inside the frame before clipping, so we use real
    # pixels instead of inventing them whenever the original allows it. This
    # clamp works the same way whether the square fits inside the frame (the
    # usual case) or is bigger than it on this axis (a tight/off-center box):
    # when side > width, width - side is negative, and clamping toward it still
    # pushes square_x0 to the position that covers the most real pixels.
    square_x0 = max(0, min(square_x0, width - side))
    square_y0 = max(0, min(square_y0, height - side))

    crop_x0 = max(0, square_x0)
    crop_y0 = max(0, square_y0)
    crop_x1 = min(width, square_x0 + side)
    crop_y1 = min(height, square_y0 + side)

    crop = (crop_x0, crop_y0, crop_x1 - crop_x0, crop_y1 - crop_y0)
    return crop, side


# ----------------------------------------------------------------------------
# 4a. Seam-carving primitives, used by the seam band operation below
# ----------------------------------------------------------------------------

def compute_energy(image):
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY).astype(np.float32)
    grad_x = cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=3)
    grad_y = cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=3)
    return np.abs(grad_x) + np.abs(grad_y)


def find_vertical_seam(energy):
    height, width = energy.shape
    cost = energy.astype(np.float64).copy()
    parent = np.zeros((height, width), dtype=np.int8)

    for row in range(1, height):
        previous = cost[row - 1]
        left = np.concatenate(([np.inf], previous[:-1]))
        right = np.concatenate((previous[1:], [np.inf]))
        stacked = np.vstack((left, previous, right))
        choice = np.argmin(stacked, axis=0)
        cost[row] += stacked[choice, np.arange(width)]
        parent[row] = choice.astype(np.int8) - 1

    seam = np.zeros(height, dtype=np.int32)
    seam[-1] = int(np.argmin(cost[-1]))
    for row in range(height - 1, 0, -1):
        seam[row - 1] = np.clip(seam[row] + parent[row, seam[row]], 0, width - 1)
    return seam


def remove_seam(array, seam):
    height, width = array.shape[:2]
    mask = np.ones((height, width), dtype=bool)
    mask[np.arange(height), seam] = False
    if array.ndim == 3:
        return array[mask].reshape(height, width - 1, array.shape[2])
    return array[mask].reshape(height, width - 1)


def insert_seam_batch(image, seam_count):
    """Insert seam_count columns, chosen as the lowest-energy paths through `image`."""
    height, width = image.shape[:2]
    working_image = image.copy()
    index_map = np.tile(np.arange(width, dtype=np.int32), (height, 1))
    duplicate_counts = np.ones((height, width), dtype=np.int32)
    rows = np.arange(height)

    for _ in range(seam_count):
        seam = find_vertical_seam(compute_energy(working_image))
        duplicate_counts[rows, index_map[rows, seam]] += 1
        working_image = remove_seam(working_image, seam)
        index_map = remove_seam(index_map, seam)

    widened = np.empty((height, width + seam_count, image.shape[2]), dtype=image.dtype)
    for row in rows:
        widened[row] = np.repeat(image[row], duplicate_counts[row], axis=0)
    return widened


def insert_vertical_seams(image, seam_count):
    """Widen by seam_count px. Large factors are done in passes of at most 50%."""
    while seam_count > 0:
        batch = min(seam_count, max(1, image.shape[1] // 2))
        image = insert_seam_batch(image, batch)
        seam_count -= batch
    return image


# ----------------------------------------------------------------------------
# 4b. Background expansion - stretch (small) or seam insertion (large)
# ----------------------------------------------------------------------------

def resize_band(band, extra):
    """Grow a background band by `extra` columns via a plain resize."""
    height, width = band.shape[:2]
    return cv2.resize(band, (width + extra, height), interpolation=cv2.INTER_LINEAR)


def seam_band(band, extra):
    """Grow a background band by `extra` columns via seam insertion."""
    return insert_vertical_seams(band, extra)


def expand_background_axis(image, left_width, right_width, target_length, band_op):
    """Widen `image` to target_length by growing the background bands with band_op.

    band_op(band, extra_px) -> widened band; used for both the stretch and seam
    methods, which only differ in how a band is widened. The product band between
    the two background bands always passes through untouched. Caller (expand_axis)
    already computed left_width/right_width and guarantees left_width+right_width > 0.
    """
    height, width = image.shape[:2]
    extra = target_length - width
    if extra <= 0:
        return image

    left_extra = int(round(extra * (left_width / (left_width + right_width))))
    right_extra = extra - left_extra

    pieces = []
    if left_width > 0:
        pieces.append(band_op(image[:, :left_width], left_extra))

    pieces.append(image[:, left_width:width - right_width])

    if right_width > 0:
        pieces.append(band_op(image[:, width - right_width:], right_extra))

    return np.hstack(pieces)


# ----------------------------------------------------------------------------
# 4c. Axis dispatcher
# ----------------------------------------------------------------------------

def expand_axis(image, keep_start, keep_end, target_length, stretch_threshold):
    """Grow one axis to target_length, product band untouched. Returns (image, method)."""
    current_length = image.shape[1]
    if target_length <= current_length:
        return image, "none"

    left_width = max(0, keep_start)
    right_width = max(0, current_length - keep_end)

    # Guard: with almost no background either method would chew into the product,
    # so extend the outer edge (the sweep colour) instead. This is also the only
    # place that handles a missing/near-empty background band - the 6% threshold
    # covers zero background too, so expand_background_axis can assume real
    # background always exists.
    if left_width + right_width < LOW_BACKGROUND_FRACTION * current_length:
        extra = target_length - current_length
        left = extra // 2
        return cv2.copyMakeBorder(image, 0, 0, left, extra - left, cv2.BORDER_REPLICATE), "pad"

    expansion = target_length / float(current_length)
    if expansion <= stretch_threshold:
        return expand_background_axis(image, left_width, right_width, target_length, resize_band), "stretch"
    return expand_background_axis(image, left_width, right_width, target_length, seam_band), "seam"


def fill_to_square(crop, protected_rect, side, stretch_threshold):
    """Expand background until crop is side x side. protected_rect is (x, y, w, h) in crop space."""
    methods = []
    x, y, w, h = protected_rect

    # Horizontal pass.
    crop, method = expand_axis(crop, x, x + w, side, stretch_threshold)
    methods.append("x:" + method)

    # Vertical pass, done on the transpose so one implementation covers both.
    transposed = np.transpose(crop, (1, 0, 2)).copy()
    transposed, method = expand_axis(transposed, y, y + h, side, stretch_threshold)
    methods.append("y:" + method)
    crop = np.transpose(transposed, (1, 0, 2)).copy()

    return crop, methods


# ----------------------------------------------------------------------------
# 5 & 6. Final resize and save
# ----------------------------------------------------------------------------

def resize_to_spec(image, min_size, max_size):
    longest = max(image.shape[0], image.shape[1])
    if longest < min_size:
        return cv2.resize(image, (min_size, min_size), interpolation=cv2.INTER_CUBIC)
    if longest > max_size:
        return cv2.resize(image, (max_size, max_size), interpolation=cv2.INTER_AREA)
    return image


def save_jpeg_srgb(bgr, output_path, quality):
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    profile = ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes()
    Image.fromarray(rgb).save(
        str(output_path), "JPEG", quality=quality, subsampling=0,
        optimize=True, icc_profile=profile,
    )


def save_debug_overlay(bgr, mask, box, margin, output_path):
    """Original with the detection mask tinted blue, the box red, the margin green."""
    overlay = bgr.copy()
    if mask is not None:
        full_mask = cv2.resize(mask, (bgr.shape[1], bgr.shape[0]), interpolation=cv2.INTER_NEAREST)
        tint = np.zeros_like(overlay)
        tint[:, :, 0] = full_mask
        overlay = cv2.addWeighted(overlay, 0.75, tint, 0.25, 0)

    x, y, w, h = box
    thickness = max(2, int(0.003 * max(bgr.shape[:2])))
    cv2.rectangle(overlay, (x, y), (x + w, y + h), (0, 0, 255), thickness)
    cv2.rectangle(overlay, (x - margin, y - margin),
                  (x + w + margin, y + h + margin), (0, 200, 0), thickness)
    cv2.imwrite(str(output_path), overlay)


# ----------------------------------------------------------------------------
# Per-image orchestration
# ----------------------------------------------------------------------------

def process_image(input_path, output_path, settings):
    original, alpha = load_image(input_path)

    detection = detect_product(
        original, alpha,
        chroma_floor=settings.chroma_threshold,
        texture_floor=settings.texture_threshold,
        analysis_limit=settings.detect_size,
        bleed_contact=settings.bleed_contact,
        shadow_edge_kernel=settings.shadow_edge_kernel,
        canny_sigma=settings.canny_sigma,
        canny_close_kernel=settings.canny_close_kernel,
    )
    mask = detection.mask

    # A detail shot has no product outline to crop to - the fabric runs off the
    # canvas. Cropping to "the product plus a margin" would mean inventing edges
    # that were never photographed, so take the biggest square that fits over the
    # most interesting part of the frame instead, with no margin since it's
    # already square - everything from here on is one shared pipeline either way.
    bleeds_off_canvas = (detection.canvas_contacts >= settings.bleed_edges
                         or detection.ring_texture > SWEEP_TEXTURE_LIMIT)
    if bleeds_off_canvas:
        box = most_salient_square(original)
        margin = 0
        detail_label = "detail crop (product bleeds off canvas)"
    else:
        box = detection.box
        margin = compute_margin(box, settings.margin)
        detail_label = None

    crop_rect, side = compute_square_crop(original.shape, box, margin)
    crop_x, crop_y, crop_w, crop_h = crop_rect
    crop = original[crop_y:crop_y + crop_h, crop_x:crop_x + crop_w].copy()

    # Product band inside the crop, padded by the safe margin - never scaled, not
    # even here: width/height are clamped against the crop edge *relative to
    # this rect's own x/y*, not just against the crop's total width/height, so
    # the protected zone can never claim to extend past the crop it lives in.
    protected_x = max(0, box[0] - margin - crop_x)
    protected_y = max(0, box[1] - margin - crop_y)
    protected_rect = (
        protected_x,
        protected_y,
        min(crop_w - protected_x, box[2] + 2 * margin),
        min(crop_h - protected_y, box[3] + 2 * margin),
    )

    canvas, methods = fill_to_square(crop, protected_rect, side, settings.stretch_threshold)
    canvas = resize_to_spec(canvas, settings.min_size, settings.max_size)
    save_jpeg_srgb(canvas, output_path, settings.quality)

    if settings.debug:
        save_debug_overlay(original, mask, box, margin,
                           output_path.with_name(output_path.stem + "_debug.jpg"))

    summary = "{name} -> {w}x{h}".format(name=input_path.name, w=canvas.shape[1], h=canvas.shape[0])
    if detail_label:
        return summary + "  " + detail_label
    return summary + "  box={box}  fill={methods}".format(
        box="{}x{}".format(box[2], box[3]), methods=",".join(methods))


# ----------------------------------------------------------------------------
# CLI
# ----------------------------------------------------------------------------

def parse_arguments():
    parser = argparse.ArgumentParser(
        description="Batch product image cropper / canvas builder.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("input_folder", help="Folder containing the images to process")
    parser.add_argument("--output-folder", help="Destination folder (default: sibling folder named 'output')")
    parser.add_argument("--margin", type=float, default=4.2, help="Margin per side, %% of the product's longest edge")
    parser.add_argument("--min-size", type=int, default=800, help="Images smaller than this become min-size squares")
    parser.add_argument("--max-size", type=int, default=2000, help="Images larger than this become max-size squares")
    parser.add_argument("--stretch-threshold", type=float, default=1.26, help="Expansion ratio above which seam carving replaces stretching")
    parser.add_argument("--quality", type=int, default=95, help="JPEG quality")
    parser.add_argument("--bleed-edges", type=int, default=2,
                        help="Canvas edges the product may run off before the image is treated as a detail shot")
    parser.add_argument("--bleed-contact", type=float, default=0.2,
                        help="Fraction of a canvas edge that must be product to count as running off it")
    parser.add_argument("--detect-size", type=int, default=ANALYSIS_LIMIT,
                        help="Resolution cap for detection; lower is faster but clips soft edges")
    parser.add_argument("--chroma-threshold", type=float, default=2.0,
                        help="Lab colour distance from the sweep that counts as product")
    parser.add_argument("--texture-threshold", type=float, default=2.0,
                        help="Local contrast that counts as product surface; lower finds fainter fabric")
    parser.add_argument("--shadow-edge-kernel", type=int, default=SHADOW_EDGE_KERNEL,
                        help="Opening size that strips a hard shadow's thin edge from texture-only "
                             "detection; lower preserves thinner real texture but tracks shadows more")
    parser.add_argument("--canny-sigma", type=float, default=CANNY_SIGMA,
                        help="Auto-Canny threshold width around the median gradient, for the "
                             "background-plane fit and the enclosed-region corroboration signal")
    parser.add_argument("--canny-close-kernel", type=int, default=CANNY_CLOSE_KERNEL,
                        help="Gap-closing size applied to the Canny edge map before flood-filling "
                             "background in from the frame border")
    parser.add_argument("--recursive", action="store_true", help="Also process images in sub-folders")
    parser.add_argument("--debug", action="store_true", help="Write a _debug.jpg per image showing the detected box")
    return parser.parse_args()


def collect_images(input_dir, recursive):
    pattern = "**/*" if recursive else "*"
    files = [p for p in sorted(input_dir.glob(pattern))
             if p.is_file() and p.suffix.lower() in SUPPORTED_EXTENSIONS]
    return files


def main():
    settings = parse_arguments()

    input_dir = Path(settings.input_folder).expanduser().resolve()
    if not input_dir.is_dir():
        print("Input folder not found: {}".format(input_dir))
        sys.exit(1)

    output_dir = (Path(settings.output_folder).expanduser().resolve()
                  if settings.output_folder else input_dir.parent / "output")
    output_dir.mkdir(parents=True, exist_ok=True)

    images = collect_images(input_dir, settings.recursive)
    if not images:
        print("No images found in {}".format(input_dir))
        sys.exit(1)

    print("Processing {} image(s) -> {}".format(len(images), output_dir))

    succeeded = 0
    for image_path in images:
        output_path = output_dir / (image_path.stem + ".jpg")
        try:
            print("  " + process_image(image_path, output_path, settings))
            succeeded += 1
        except Exception as error:  # keep the batch running
            print("  FAILED {}: {}".format(image_path.name, error))

    print("Done: {}/{} written to {}".format(succeeded, len(images), output_dir))

if __name__ == "__main__":
    main()