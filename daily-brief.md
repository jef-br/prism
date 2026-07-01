**Tx_CenterAndStretch.cs non-negative border bug**

Given are 2 images:
the margin in prism_config reads 0.042 representing a 4.2% minimum margin on each side

- a.jpg
  - original size: 1500x2000
  - bounding box: x:400,y:100,w:400,h:1800
  - output size: 1948x1948

This case requires background extension along two sides: one stretch left, one stretch right

- final output size = original - calculated output size rounded down to the next even number - 2px. The last factor handles antialiasing issues.
- Solving the image height is easy: cut pixels from the image top and bottom evenly.
- 1948px = 1800 + 2* margin → 1951.2px → rounded down = 1951 → to the next even number = 1950 → -2px → 1948px

- 1500-1948 causes a negative number. The absolute value of that negative number likely gives us the needed offset.
- bbox.x += Math.abs((1500-1948)/2) → 224px
- bbox.y += Math.abs((2000-1948)/2) → 26px



------------------ PHOTOSHOP SCRIPT ADDITION TO CLARIFY DESIRED BEHAVIOR:
 if(DEBUG)$.writeln(this.productMargin + " ; " + this.scriptTag);

        var lyrClahe = "CLAHE";

        recipes.prepareForProcessing(this.originalImage);
        recipes.CLAHE(this.originalImage, lyrClahe);

        /* crop to object using Sensei AI */
        psh.autoSelect(true);
        //TODO: write alternative in case when autoselect fails (cropsquareflatbackground)
        //... if (psh.hasSelection()) --> cropsquareflatbackground with same margin
        actDoc.crop(actDoc.selection.bounds);
        psh.deselect();
        psh.deleteLayerByName(lyrClahe);

        /* resize image canvas to final output size  */
        var productWidth = actDoc.width.value;
        var productHeight = actDoc.height.value;
        var finalImageSize = psCfg.calculateOutputsize(productWidth, productHeight, this.productMargin);
        var finalProductSize = finalImageSize - (finalImageSize * this.productMargin * 2);

        psh.fitImage(finalProductSize, finalProductSize);
        actDoc.resizeCanvas(finalImageSize, finalImageSize, AnchorPosition.MIDDLECENTER);
        actDoc.resizeImage(finalImageSize, finalImageSize, VeepeeSettings.dpi);

        /* stretch background */
        var productCoordinates = {
            left: (finalImageSize - productWidth) / 2,
            top: (finalImageSize - productHeight) / 2,
            right: productWidth + (finalImageSize - productWidth) / 2,
            bottom: productHeight + (finalImageSize - productHeight) / 2
        }
        var doCleanBackground = false;
        recipes.stretchBackground(productCoordinates, doCleanBackground);
        psh.duplicateCurrentLayer("Copy to avoid single layer flattening bug");
        psh.flattenImage();

----------

constructor.prototype.stretchBackground = function (productBounds, doCleanBackground) {
        psh.rasterizeLayer();
        psh.snapshotCreate("pre stretch");

        var actDoc = app.activeDocument,
            areaBoundOffset = 2,
            stretch = {
                left: actDoc.activeLayer.bounds[0].value > 0,
                top: actDoc.activeLayer.bounds[1].value > 0,
                right: actDoc.activeLayer.bounds[2].value < actDoc.width.value,
                bottom: actDoc.activeLayer.bounds[3].value < actDoc.height.value
            };


        if (DEBUG && (stretch.left || stretch.top || stretch.right || stretch.bottom)) {
            $.writeln("stretch: " + (stretch.left ? "left, " : "") + (stretch.top ? "top, " : "") + (stretch.right ? "right, " : "") + (stretch.bottom ? "bottom" : ""))
        };

        if (stretch.left) { this.stretchLeft(productBounds.left, areaBoundOffset); }
        if (stretch.top) { this.stretchTop(productBounds.top, areaBoundOffset); }
        if (stretch.right) { this.stretchRight(productBounds.right, areaBoundOffset); }
        if (stretch.bottom) { this.stretchBottom(productBounds.bottom, areaBoundOffset); }

        psh.deselect();
        psh.snapshotCreate("stretched");
        if (doCleanBackground) {
            this.cleanBackground(productBounds, areaBoundOffset * 10);
            psh.snapshotCreate("stretched and cleaned");
        }
        psh.snapshotDelete("pre stretch");
    };
    constructor.prototype.stretchLeft = function (productEdge, areaBoundOffset) {
        var area = [
            parseInt(app.activeDocument.activeLayer.bounds[0].value + areaBoundOffset),
            0,
            parseInt(productEdge - 2 * areaBoundOffset),
            app.activeDocument.height.value
        ];
        area[2] = (area[2] < area[0]) ? area[0] + 2 : area[2];
        var scalingFactor = 102 + 101 * area[0] / (area[2] - area[0]);
        psh.selectRectangle(area[0], area[1], area[2], area[3], 0, false);
        if (psh.hasSelection) {
            psh.transformScale(area[2], 0, 0, 0, scalingFactor, 100);
        }
    };
    constructor.prototype.stretchTop = function (productEdge, areaBoundOffset) {
        var area = [
            0,
            parseInt(app.activeDocument.activeLayer.bounds[1].value + areaBoundOffset),
            app.activeDocument.width.value,
            parseInt(productEdge - 2 * areaBoundOffset)
        ];
        area[3] = (area[3] < area[1]) ? area[1] + 2 : area[3];
        var scalingFactor = 102 + ((101 * area[1]) / (area[3] - area[1]));
        psh.selectRectangle(area[0], area[1], area[2], area[3], 0, false);
        if (psh.hasSelection) {
            psh.transformScale(0, area[3], 0, 0, 100, scalingFactor);
        }
    };
    constructor.prototype.stretchRight = function (productEdge, areaBoundOffset) {
        var area = [
            parseInt(productEdge + 2 * areaBoundOffset),
            0,
            parseInt(app.activeDocument.activeLayer.bounds[2].value - areaBoundOffset),
            app.activeDocument.height.value
        ];
        area[0] = (area[2] < area[0]) ? area[2] - 2 : area[0];
        var scalingFactor = 102 + (100 * (app.activeDocument.width - area[2])) / (area[2] - area[0]);
        psh.selectRectangle(area[0], area[1], area[2], area[3], 0, false);
        if (psh.hasSelection) {
            psh.transformScale(area[0], 0, 0, 0, scalingFactor, 100);
        }
    };
    constructor.prototype.stretchBottom = function (productEdge, areaBoundOffset) {
        var area = [
            0,
            parseInt(productEdge + 2 * areaBoundOffset),
            app.activeDocument.width.value,
            parseInt(app.activeDocument.activeLayer.bounds[3].value - areaBoundOffset)
        ];
        area[1] = (area[3] < area[1] ? area[3] - 2 : area[1]);
        var scalingFactor = 102 + 100 * (app.activeDocument.height - area[3]) / (area[3] - area[1]);
        psh.selectRectangle(area[0], area[1], area[2], area[3], 0, false);
        if (psh.hasSelection) {
            psh.transformScale(0, area[1], 0, 0, 100, scalingFactor);
        }
    };