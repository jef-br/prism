








## To delete: ImageRecord*.cs files intended to perform their tasks
- Transform\Engine\BackgroundType.cs → To rewire and delete. BackgroundType is set by an analyzer over in `jb\src\core\Services\Matching\Analyzers` The result is passed on via the Lambda record.
- Transform\Engine\ImageTransformationResult.cs → Should be done using  `jb\src\core\Models\ImageRecord_OUTPUT.cs` 


## Fix multiple config files
A generic json config loader `jb\src\core\config\ConfigLoader.cs` (already exists) should be used instead of a .cs file per config.
This loader also replaces the existing `jb\src\core\config\PrismConfigLocator.cs`
This loader needs to be usable by all services and work by only loading the relevant part of a json file.
For the files inside Transform, this path is `jb\src\core\config\transform_Config.json`
For the rest of PRISM, a similar loading strategy is used.

The full implementation of this loader is a huge task. Plan it well and include proper and thorough checks and implementation-verification.

- Transform\Engine\BgStretchConfig.cs → Load the "BgStretch" part of `transform_Config.json`
- Transform\Engine\DetailCropperConfig.cs → Load the "DetailCropper" part of `transform_Config.json`
- Transform\Engine\HeadCutterConfig.cs →  Load the "HeadCutter" part of `transform_Config.json`
- Transform\Engine\ProblemImageProcessorConfig.cs → same principle as mentioned above
- Transform\Engine\TransformConfig.cs → same as above
- Transform\Engine\CropTransformSettings.cs → same as above
- Transform\Engine\LowContrastEnhancementConfig.cs → same as above

## These files should not be hidden, these are the key files for human developers to work on.
Keep them in `\Transform\Engine` Put any auxiliary class to them inside `Transform/Engine/Utils`
- Transform\Engine\Tx_CenterAndStretch.cs → Tx_CenterAndStretch.cs
- Transform\Engine\Tx_CropSquare.cs → Tx_CropSquare.cs
- Transform\Engine\Tx_DetailCropper.cs → Tx_DetailCropper.cs
- Transform\Engine\Tx_ProblemImageProcessor.cs → Tx_ProblemImageProcessor.cs
- Transform\Engine\Tx_util_BgStretch.cs → Tx_util_BgStretch.cs
- Transform\Engine\Tx_util_HeadCutter.cs → Tx_util_HeadCutter.cs
    
## move these files
- Transform\Engine\TransformationStatus.cs → Transform\Enum\TransformationStatus.cs
- Transform\Engine\processingtools\Tx_LowContrastEnhancement.cs → Utils\Tx_LowContrastEnhancement.cs

## These are ok
- Transform\ImageTransformer.cs → ImageTransformer.cs
- Transform\ITransformService.cs → ITransformService.cs
- Transform\TransformResult.cs → TransformResult.cs
- Transform\TransformService.cs → TransformService.cs
- Transform\Engine\IImageTransformation.cs → IImageTransformation.cs