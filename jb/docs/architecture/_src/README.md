# Diagram sources

The nine `.drawio.svg` files in `jb/docs/architecture/` are the artifacts. Each one is an ordinary
SVG — it renders as a picture in GitHub, VS Code, a browser, anywhere — whose root element also
carries a `content` attribute holding the full mxfile document. draw.io opens that as a normal
editable diagram, edits it, and saves back to the same file.

So there are two ways to change a diagram, and they do not mix well:

**Edit it in draw.io.** Open the `.drawio.svg`, change it, save. This is the right choice for a small
fix — a wording change, moving a box. From that point the file is ahead of this folder, and re-running
the generator would throw the edit away.

**Regenerate.** `build.py` describes every diagram as nodes and edges and emits both halves — the
rendered SVG and the embedded mxfile — from one description, so the picture and the editable model
cannot drift apart. This is the right choice for a structural change, or for a change that touches
several diagrams at once.

```
python3 jb/docs/architecture/_src/build.py                  # all nine
python3 jb/docs/architecture/_src/build.py system-context   # just one
```

`drawio.py` is the writer: palette, node and edge shapes, orthogonal routing, and the two output
paths. `build.py` holds one function per diagram.

Nothing in the build pipeline runs this — no CI step, no MSBuild target. It exists so the diagrams
stay cheap to redraw.

## Checking a change

There is no renderer in the repo. To eyeball a diagram, open the `.svg` in a browser, or rasterize it
with any headless Chromium:

```
chrome --headless --disable-gpu --screenshot=out.png --window-size=1340,700 \
       --default-background-color=FFFFFFFF file:///abs/path/system-context.drawio.svg
```

To confirm a file is still valid on both halves — the SVG parses, and draw.io will be able to open it:

```python
import xml.etree.ElementTree as ET
root = ET.parse("system-context.drawio.svg").getroot()
ET.fromstring(root.get("content"))          # raises if the embedded mxfile is malformed
```
