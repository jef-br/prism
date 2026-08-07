                                    # SPACINI29 image routing list

                                    **Purpose.** One row per SPACINI29 image. You fill in **Intended route** by eye. That column is
                                    blessed knowledge — it becomes the ground truth [[T-5010]]'s routing work is measured against. No
                                    agent may write to it, infer it, or "correct" it.

                                    **Status:** current-route column is pending an evidence-harness run — it was blocked behind a
                                    serialized full test suite (parallel GPU runs reproduce T-4942's defect). You can start on the
                                    intended-route column now; looking at the image tells you the intended route regardless of what the
                                    pipeline does today.

                                    ## How to fill this in

                                    Put one of these in **Intended route**:

                                    | Value | Means |
                                    |---|---|
                                    | `Tx_CenterAndStretch` | Subject touches no edge. Centre it on a square canvas, stretch background to fill. |
                                    | `Tx_DetailCropper` | Subject touches one or more edges. Pin the touched edge, centre the box on the free axis, squarify, background-stretch if the canvas needed extending. |
                                    | `Tx_CropSquare` | A plain square crop at dead centre of the canvas is genuinely what this image wants. Note: this does **not** centre on the subject and does **not** stretch background. |
                                    | `Tx_ProblemImageProcessor` | Nothing geometric should happen. Proportional resize only. |
                                    | `KO` | This image should be rejected, not transformed. |

                                    **Notes** is free text — use it when the row needs a reason, or when none of the five values fit.

                                    ## Rows

                                    | Image | Current route | Intended route | Notes |
                                    |---|---|---|---|
                                    | 20213024_46_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 20213024_46_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 20213041_65_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 20213041_65_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 20213548_12_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 20213548_12_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211008_02_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211008_02_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211018_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211018_56_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211027_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211027_56_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211041_02_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211041_02_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211041_03_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211041_03_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211056_35_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211056_35_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211064_03_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211064_03_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211064_23_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211064_23_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211066_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211066_56_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211080_06_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211080_06_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211080_83_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211080_83_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211088_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211088_56_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211089_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211089_56_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211094_06_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211094_06_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211094_35_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211094_35_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211095_35_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211095_35_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211104_08_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211104_08_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211104_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211104_56_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211108_03_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211108_03_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211115_08_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211115_08_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211116_08_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211116_08_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211121_03_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211121_03_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211121_23_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211121_23_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211527_56_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23211527_56_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211527_83_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23211527_83_B.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23231006_02_A.jpg | _(pending harness run)_ | Route-1 |  |
                                    | 23231006_02_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231007_02_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231007_02_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231023_52_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231023_52_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231038_02_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231038_02_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231038_03_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231038_03_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231040_02_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231040_02_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231072_08_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231072_08_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231073_08_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231073_08_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 23231096_35_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211507_76_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211507_76_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211508_76_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211508_76_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211508_96_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211508_96_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211511_76_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211511_86_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211511_96_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211513_76_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211513_76_B.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211514_76_A.jpg | _(pending harness run)_ | Route-2 |  |
                                    | 24211514_76_B.jpg | _(pending harness run)_ | Route-2 |  |


                                    **ROUTING INFO**
                                    * Route-1: all these images have 1 intersection: the Top edge.
                                      * This needs to happen:
                                        * A margin needs to be applied to the opposing side of the intersection. Ie the bottom of the image.
                                        * The result has to be that the image subject is <margin> removed from the bottom edge. <margin> comes from `transform_config.json` under `Crop.WhiteSpaceMargin`. (0.042, ie a 4.2% margin) If the subject is 1000px tall, the margin would be 42px
                                        * <margin> cannot be applied to the top edge and need not be applied to the left and right.
                                        * Center the subjects bounding box horizontally.
                                        * squarify the image canvas
                                        * stretch the background using `Tx_util_BgStretch.cs`
                                    * Route-2: All images have 2 intersections: Top and Bottom. This needs to happen:
                                      * Center the subjects bounding box horizontally.
                                      * squarify the image canvas
                                      * stretch the background using `Tx_util_BgStretch.cs`
                                      * resize the image to its output size