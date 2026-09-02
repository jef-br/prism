"""Minimal .drawio.svg writer.

A .drawio.svg is an ordinary SVG whose root element carries a `content` attribute
holding the (XML-escaped) mxfile document. draw.io opens such a file as a fully
editable diagram; every other viewer just renders the SVG.

This module emits both halves from one node/edge description, so the picture and
the editable model can never disagree. See README.md in this folder.
"""

from __future__ import annotations

import html
from dataclasses import dataclass, field

FONT = "Helvetica,Arial,sans-serif"

# Semantic palette. Light-ground fills with a matching darker stroke; every
# diagram paints its own white page rect so it reads on a dark site theme too.
PALETTE = {
    "core":     ("#E7EEF9", "#3D6DB5", "#16324F"),
    "service":  ("#EFE8F7", "#7A5AA8", "#3A2456"),
    "lib":      ("#E6F2EA", "#3F8A57", "#1D4429"),
    "contract": ("#FBF894", "#B5851F", "#5A4008"),
    "record":   ("#FDF2DC", "#B5851F", "#5A4008"),
    "external": ("#EEF0F2", "#8996A3", "#3E4C59"),
    "client":   ("#E3F1F1", "#3F8489", "#17403F"),
    "ko":       ("#FBE7E4", "#C0483A", "#5E1E16"),
    "plain":    ("#FFFFFF", "#8996A3", "#1F2933"),
    "band":     ("#F7F9FB", "#C6D0DA", "#52606D"),
}
PALETTE["contract"] = ("#FDF6D8", "#B5851F", "#5A4008")

INK = "#1F2933"
MUTED = "#52606D"


def esc(text: str) -> str:
    return (text.replace("&", "&amp;").replace("<", "&lt;")
                .replace(">", "&gt;").replace('"', "&quot;"))


@dataclass
class Node:
    id: str
    label: str
    x: float
    y: float
    w: float
    h: float
    kind: str = "plain"
    shape: str = "box"          # box | round | pill | cyl | note | hex
    font: int = 12
    bold: bool = False
    dashed: bool = False
    align: str = "center"       # center | left
    sub: str = ""               # smaller second-tier line, drawn under label


@dataclass
class Edge:
    src: str
    dst: str
    label: str = ""
    exit: str = "e"             # e | w | n | s
    entry: str = "w"
    dashed: bool = False
    color: str = "#5A6B7B"
    label_dx: float = 0.0
    label_dy: float = 0.0
    arrow: bool = True
    waypoints: list[tuple[float, float]] = field(default_factory=list)


@dataclass
class Band:
    """Titled background container drawn behind the nodes."""
    id: str
    title: str
    x: float
    y: float
    w: float
    h: float
    color: str = "#C6D0DA"
    fill: str = "#F7F9FB"


class Diagram:
    def __init__(self, name: str, width: int, height: int, title: str = "", subtitle: str = ""):
        self.name = name
        self.width = width
        self.height = height
        self.title = title
        self.subtitle = subtitle
        self.bands: list[Band] = []
        self.nodes: dict[str, Node] = {}
        self.edges: list[Edge] = []
        self.notes: list[tuple[float, float, str, str, int]] = []  # x, y, text, anchor, size

    # -- authoring -------------------------------------------------------
    def band(self, *a, **kw) -> Band:
        b = Band(*a, **kw)
        self.bands.append(b)
        return b

    def node(self, *a, **kw) -> Node:
        n = Node(*a, **kw)
        self.nodes[n.id] = n
        return n

    def edge(self, *a, **kw) -> Edge:
        e = Edge(*a, **kw)
        self.edges.append(e)
        return e

    def note(self, x, y, text, anchor="start", size=11):
        self.notes.append((x, y, text, anchor, size))

    # -- geometry --------------------------------------------------------
    @staticmethod
    def _port(n: Node, side: str) -> tuple[float, float]:
        return {
            "e": (n.x + n.w, n.y + n.h / 2),
            "w": (n.x, n.y + n.h / 2),
            "n": (n.x + n.w / 2, n.y),
            "s": (n.x + n.w / 2, n.y + n.h),
        }[side]

    def _route(self, e: Edge) -> list[tuple[float, float]]:
        s = self._port(self.nodes[e.src], e.exit)
        t = self._port(self.nodes[e.dst], e.entry)
        if e.waypoints:
            return [s, *e.waypoints, t]

        horiz = {"e", "w"}
        if e.exit in horiz and e.entry in horiz:
            if abs(s[1] - t[1]) < 0.5:
                return [s, t]
            mx = (s[0] + t[0]) / 2
            return [s, (mx, s[1]), (mx, t[1]), t]
        if e.exit not in horiz and e.entry not in horiz:
            if abs(s[0] - t[0]) < 0.5:
                return [s, t]
            my = (s[1] + t[1]) / 2
            return [s, (s[0], my), (t[0], my), t]
        if e.exit in horiz:
            return [s, (t[0], s[1]), t]
        return [s, (s[0], t[1]), t]

    @staticmethod
    def _mid(pts: list[tuple[float, float]]) -> tuple[float, float]:
        total = sum(abs(pts[i + 1][0] - pts[i][0]) + abs(pts[i + 1][1] - pts[i][1])
                    for i in range(len(pts) - 1))
        want, run = total / 2, 0.0
        for i in range(len(pts) - 1):
            seg = abs(pts[i + 1][0] - pts[i][0]) + abs(pts[i + 1][1] - pts[i][1])
            if run + seg >= want and seg:
                f = (want - run) / seg
                return (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * f,
                        pts[i][1] + (pts[i + 1][1] - pts[i][1]) * f)
            run += seg
        return pts[len(pts) // 2]

    # -- SVG -------------------------------------------------------------
    def _svg_body(self) -> str:
        out: list[str] = []
        out.append(f'<rect x="0" y="0" width="{self.width}" height="{self.height}" fill="#FFFFFF"/>')

        if self.title:
            out.append(f'<text x="28" y="42" font-family="{FONT}" font-size="20" '
                       f'font-weight="600" fill="{INK}">{esc(self.title)}</text>')
        if self.subtitle:
            out.append(f'<text x="28" y="64" font-family="{FONT}" font-size="12" '
                       f'fill="{MUTED}">{esc(self.subtitle)}</text>')

        for b in self.bands:
            out.append(f'<rect x="{b.x}" y="{b.y}" width="{b.w}" height="{b.h}" rx="8" ry="8" '
                       f'fill="{b.fill}" stroke="{b.color}" stroke-width="1" stroke-dasharray="6 4"/>')
            if b.title:
                out.append(f'<text x="{b.x + 12}" y="{b.y + 20}" font-family="{FONT}" font-size="11" '
                           f'font-weight="600" fill="{MUTED}" letter-spacing="0.6">{esc(b.title)}</text>')

        for e in self.edges:
            pts = self._route(e)
            d = " ".join(f"{'M' if i == 0 else 'L'} {x:.1f} {y:.1f}" for i, (x, y) in enumerate(pts))
            dash = ' stroke-dasharray="6 4"' if e.dashed else ""
            marker = f' marker-end="url(#arrow-{e.color.lstrip("#")})"' if e.arrow else ""
            out.append(f'<path d="{d}" fill="none" stroke="{e.color}" stroke-width="1.6"{dash}{marker}/>')
            if e.label:
                mx, my = self._mid(pts)
                mx += e.label_dx
                my += e.label_dy
                lines = e.label.split("\n")
                wid = max(len(l) for l in lines) * 5.9 + 12
                hgt = len(lines) * 13 + 4
                out.append(f'<rect x="{mx - wid / 2:.1f}" y="{my - hgt / 2:.1f}" width="{wid:.1f}" '
                           f'height="{hgt:.1f}" rx="3" ry="3" fill="#FFFFFF" fill-opacity="0.92"/>')
                base = my - hgt / 2 + 13
                out.append(f'<text font-family="{FONT}" font-size="10.5" fill="{MUTED}" text-anchor="middle">')
                for i, l in enumerate(lines):
                    out.append(f'<tspan x="{mx:.1f}" y="{base + i * 13:.1f}">{esc(l)}</tspan>')
                out.append("</text>")

        for n in self.nodes.values():
            fill, stroke, text = PALETTE[n.kind]
            rx = {"box": 3, "round": 10, "pill": n.h / 2, "note": 3, "cyl": 8, "hex": 3}[n.shape]
            dash = ' stroke-dasharray="6 4"' if n.dashed else ""
            out.append(f'<rect x="{n.x}" y="{n.y}" width="{n.w}" height="{n.h}" rx="{rx}" ry="{rx}" '
                       f'fill="{fill}" stroke="{stroke}" stroke-width="1.4"{dash}/>')

            lines = [(l, n.font, n.bold) for l in n.label.split("\n")]
            if n.sub:
                lines += [(l, max(9, n.font - 2), False) for l in n.sub.split("\n")]
            lh = n.font + 4
            block = len(lines) * lh
            base = n.y + n.h / 2 - block / 2 + lh - 4
            if n.align == "left":
                tx, anchor = n.x + 12, "start"
            else:
                tx, anchor = n.x + n.w / 2, "middle"
            for i, (l, size, bold) in enumerate(lines):
                weight = ' font-weight="600"' if bold else ""
                colour = text if i < len(n.label.split("\n")) else MUTED
                out.append(f'<text x="{tx:.1f}" y="{base + i * lh:.1f}" font-family="{FONT}" '
                           f'font-size="{size}"{weight} fill="{colour}" text-anchor="{anchor}" xml:space="preserve">'
                           f'{esc(l)}</text>')

        for x, y, t, anchor, size in self.notes:
            for i, line in enumerate(t.split("\n")):
                out.append(f'<text x="{x}" y="{y + i * (size + 4)}" font-family="{FONT}" '
                           f'font-size="{size}" fill="{MUTED}" text-anchor="{anchor}">{esc(line)}</text>')

        return "\n".join(out)

    # -- mxGraph ---------------------------------------------------------
    def _mxfile(self) -> str:
        cells: list[str] = []

        def style_for(n: Node) -> str:
            fill, stroke, text = PALETTE[n.kind]
            base = "rounded=1;arcSize=14;" if n.shape in ("round", "cyl") else "rounded=0;"
            if n.shape == "pill":
                base = "rounded=1;arcSize=50;"
            bits = [base, "whiteSpace=wrap;html=1;",
                    f"fillColor={fill};strokeColor={stroke};fontColor={text};",
                    f"fontSize={n.font};", f"align={n.align};",
                    "verticalAlign=middle;", f"fontFamily={FONT.split(',')[0]};"]
            if n.bold:
                bits.append("fontStyle=1;")
            if n.dashed:
                bits.append("dashed=1;dashPattern=6 4;")
            if n.align == "left":
                bits.append("spacingLeft=8;")
            return "".join(bits)

        if self.title:
            cells.append(
                f'<mxCell id="__title" value="{esc(self.title)}" style="text;html=1;align=left;'
                f'verticalAlign=middle;fontSize=20;fontStyle=1;fontColor={INK};" vertex="1" parent="1">'
                f'<mxGeometry x="24" y="20" width="{self.width - 48}" height="30" as="geometry"/></mxCell>')
        if self.subtitle:
            cells.append(
                f'<mxCell id="__subtitle" value="{esc(self.subtitle)}" style="text;html=1;align=left;'
                f'verticalAlign=middle;fontSize=12;fontColor={MUTED};" vertex="1" parent="1">'
                f'<mxGeometry x="24" y="52" width="{self.width - 48}" height="20" as="geometry"/></mxCell>')

        for b in self.bands:
            cells.append(
                f'<mxCell id="{b.id}" value="{esc(b.title)}" style="rounded=1;arcSize=6;whiteSpace=wrap;'
                f'html=1;fillColor={b.fill};strokeColor={b.color};dashed=1;dashPattern=6 4;'
                f'verticalAlign=top;align=left;spacingLeft=8;spacingTop=2;fontSize=11;fontStyle=1;'
                f'fontColor={MUTED};" vertex="1" parent="1">'
                f'<mxGeometry x="{b.x}" y="{b.y}" width="{b.w}" height="{b.h}" as="geometry"/></mxCell>')

        for n in self.nodes.values():
            value = n.label.replace("\n", "<br>")
            if n.sub:
                value += "<br>" + "<br>".join(
                    f'<font style="font-size:{max(9, n.font - 2)}px;color:{MUTED}">{l}</font>'
                    for l in n.sub.split("\n"))
            cells.append(
                f'<mxCell id="{n.id}" value="{esc(value)}" style="{style_for(n)}" vertex="1" parent="1">'
                f'<mxGeometry x="{n.x}" y="{n.y}" width="{n.w}" height="{n.h}" as="geometry"/></mxCell>')

        anchor = {"e": (1, 0.5), "w": (0, 0.5), "n": (0.5, 0), "s": (0.5, 1)}
        for i, e in enumerate(self.edges):
            ex, ey = anchor[e.exit]
            nx, ny = anchor[e.entry]
            st = ("edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;jettySize=auto;orthogonalLoop=1;"
                  f"exitX={ex};exitY={ey};exitDx=0;exitDy=0;entryX={nx};entryY={ny};entryDx=0;entryDy=0;"
                  f"strokeColor={e.color};fontSize=10;fontColor={MUTED};labelBackgroundColor=#FFFFFF;")
            if e.dashed:
                st += "dashed=1;dashPattern=6 4;"
            if not e.arrow:
                st += "endArrow=none;"
            geo = "<mxGeometry relative=\"1\" as=\"geometry\">"
            if e.waypoints:
                geo += "<Array as=\"points\">" + "".join(
                    f'<mxPoint x="{x}" y="{y}"/>' for x, y in e.waypoints) + "</Array>"
            geo += "</mxGeometry>"
            cells.append(
                f'<mxCell id="edge{i}" value="{esc(e.label.replace(chr(10), "<br>"))}" style="{st}" '
                f'edge="1" parent="1" source="{e.src}" target="{e.dst}">{geo}</mxCell>')

        for j, (x, y, t, al, size) in enumerate(self.notes):
            cells.append(
                f'<mxCell id="note{j}" value="{esc(t.replace(chr(10), "<br>"))}" '
                f'style="text;html=1;align={"left" if al == "start" else "right"};verticalAlign=top;'
                f'fontSize={size};fontColor={MUTED};" vertex="1" parent="1">'
                f'<mxGeometry x="{x if al == "start" else x - 320}" y="{y - 12}" '
                f'width="{(self.width - x - 24) if al == "start" else 320}" '
                f'height="{(t.count(chr(10)) + 1) * (size + 4) + 8}" as="geometry"/></mxCell>')

        model = (f'<mxGraphModel dx="1200" dy="800" grid="0" gridSize="10" guides="1" tooltips="1" '
                 f'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{self.width}" '
                 f'pageHeight="{self.height}" math="0" shadow="0"><root>'
                 f'<mxCell id="0"/><mxCell id="1" parent="0"/>{"".join(cells)}</root></mxGraphModel>')

        return (f'<mxfile host="prism-docs" type="device" version="24.7.17">'
                f'<diagram id="{self.name}" name="{esc(self.name)}">{model}</diagram></mxfile>')

    # -- output ----------------------------------------------------------
    def render(self) -> str:
        arrows = {e.color for e in self.edges}
        defs = "".join(
            f'<marker id="arrow-{c.lstrip("#")}" viewBox="0 0 10 10" refX="9" refY="5" '
            f'markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
            f'<path d="M 0 1 L 10 5 L 0 9 z" fill="{c}"/></marker>' for c in arrows)
        content = esc(self._mxfile())
        return (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
                f'width="{self.width}" height="{self.height}" viewBox="0 0 {self.width} {self.height}" '
                f'content="{content}">\n<defs>{defs}</defs>\n{self._svg_body()}\n</svg>\n')

    def write(self, path: str) -> None:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(self.render())


def label_width(text: str, size: int = 12) -> float:
    """Rough advance width; useful when sizing a node to its longest line."""
    return max(len(l) for l in text.split("\n")) * size * 0.56
