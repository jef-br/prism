using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace UpscalerTestClient;

// Standalone harness that exercises the Real-ESRGAN upscaler in isolation by loading
// Real-ESRGAN_x2plus.onnx DIRECTLY (ONNX Runtime + OpenCvSharp) — no Prism.Core, no pipeline,
// no API. Given an image it produces an upscaled copy on the Desktop with a "-upscaled" suffix.
//
//   Interactive : dotnet run --project test/UpscalerTestClient
//                 (no --image -> opens a file-picker to "ask for an image")
//   Batch       : dotnet run --project test/UpscalerTestClient -- --image "a.jpg" --image "b.jpg"
//
// Args: --image <path> (repeatable, optional), --scale <double> (default 2.0), --out <dir> (default Desktop),
//       --model <path> (optional; otherwise resolved from the source tree).
//
// The tiling / normalization mirrors jb/src/core/Images/Upscale/Upscaler_g_p_u.cs so the standalone
// result matches the pipeline's: Real-ESRGAN fixed x2 in overlapping 64x64 tiles, then Lanczos4 to
// reach the exact requested scale.
internal static class Program {
    private const string ModelRelativePath = "Images/Upscale/ONNX/Real-ESRGAN_x2plus.onnx";
    private const string TensorInput  = "input";
    private const string TensorOutput = "output";
    private const int    SrScaleInt   = 2;      // model is fixed x2
    private const double SrScale      = 2.0;
    private const int    TileOverlap  = 8;      // source-pixel border discarded at tile seams

    [STAThread]
    private static int Main( string[] args ) {
        var images = new List<string>();
        double scale = 2.0;
        string outDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string? modelPath = null;

        for (int i = 0; i < args.Length; i++) {
            switch (args[i]) {
                case "--image" when i + 1 < args.Length: images.Add(args[++i]); break;
                case "--scale" when i + 1 < args.Length: scale = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--out"   when i + 1 < args.Length: outDir = args[++i]; break;
                case "--model" when i + 1 < args.Length: modelPath = args[++i]; break;
                default: Console.Error.WriteLine($"Unknown/incomplete arg: {args[i]}"); return 2;
            }
        }

        // No images supplied -> ask for one via the OS file-picker dialog.
        if (images.Count == 0) {
            string? picked = AskForImage();
            if (picked is null) { Console.WriteLine("No image selected. Nothing to do."); return 0; }
            images.Add(picked);
        }

        modelPath ??= FindModelInSourceTree(ModelRelativePath);
        if (modelPath is null || !File.Exists(modelPath)) {
            Console.Error.WriteLine($"Model not found (searched for jb/src/core/{ModelRelativePath}). Pass --model <path>.");
            return 2;
        }
        Console.WriteLine($"Model         : {modelPath}");
        Console.WriteLine($"Scale factor  : {scale}");

        int tileH, tileW;
        using InferenceSession session = LoadSession(modelPath, out tileH, out tileW);
        Console.WriteLine($"Tile size     : {tileW}x{tileH}");
        Console.WriteLine();

        int failures = 0;
        foreach (string image in images)
            if (!ProcessOne(session, tileH, tileW, image, scale, outDir)) failures++;

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Opens a Windows file-picker so the user can choose an image to upscale.</summary>
    private static string? AskForImage() {
        using var dialog = new OpenFileDialog {
            Title = "Select an image to upscale",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    /// <summary>Loads the ONNX model with the DirectML execution provider and reads its fixed tile shape.</summary>
    private static InferenceSession LoadSession( string modelPath, out int tileHeight, out int tileWidth ) {
        var opts = new SessionOptions();
        opts.AppendExecutionProvider_DML(0);
        var session = new InferenceSession(modelPath, opts);

        int[] dims = session.InputMetadata[TensorInput].Dimensions;
        tileHeight = dims[2] > 0 ? dims[2] : 0;   // 0 => dynamic (whole image in one tile)
        tileWidth  = dims[3] > 0 ? dims[3] : 0;
        return session;
    }

    /// <summary>Walks up from the running binary looking for jb/src/core/{relative}.</summary>
    private static string? FindModelInSourceTree( string relative ) {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) {
            string candidate = Path.Combine(dir.FullName, "jb", "src", "core", relative);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Upscales one image and writes &lt;name&gt;-upscaled.jpg to the output directory.</summary>
    private static bool ProcessOne( InferenceSession session, int tileH, int tileW, string imagePath, double scale, string outDir ) {
        if (!File.Exists(imagePath)) {
            Console.Error.WriteLine($"KO  {imagePath} — file not found");
            return false;
        }

        try {
            byte[] input = File.ReadAllBytes(imagePath);

            var sw = Stopwatch.StartNew();
            using Mat srcBgr = Cv2.ImDecode(input, ImreadModes.Color);
            using Mat srBgr  = RunTiled(session, tileH, tileW, srcBgr);      // fixed x2
            using Mat finalBgr = ApplyLanczos4(srcBgr, srBgr, scale);        // top-up to exact scale
            Cv2.ImEncode(".jpg", finalBgr, out byte[] output);
            sw.Stop();

            string outName = Path.GetFileNameWithoutExtension(imagePath) + "-upscaled.jpg";
            string outPath = Path.Combine(outDir, outName);
            File.WriteAllBytes(outPath, output);

            Console.WriteLine($"OK  {Path.GetFileName(imagePath)}  {srcBgr.Cols}x{srcBgr.Rows} -> {finalBgr.Cols}x{finalBgr.Rows}  ({sw.ElapsedMilliseconds} ms)");
            Console.WriteLine($"    -> {outPath}");
            return true;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"KO  {Path.GetFileName(imagePath)} — {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Splits src into overlapping tiles sized to the model's fixed input shape (whole image in one tile
    /// when dynamic), runs each through the session, and stitches the trusted centers into the x2 result.
    /// </summary>
    private static Mat RunTiled( InferenceSession session, int tileHModel, int tileWModel, Mat src ) {
        bool tiling = tileHModel > 0 && tileWModel > 0;
        int tileH = tiling ? tileHModel : src.Rows;
        int tileW = tiling ? tileWModel : src.Cols;
        int overlap = tiling ? TileOverlap : 0;

        int stepH = Math.Max(1, tileH - 2 * overlap);
        int stepW = Math.Max(1, tileW - 2 * overlap);

        int tilesY = Math.Max(1, (int)Math.Ceiling(src.Rows / (double)stepH));
        int tilesX = Math.Max(1, (int)Math.Ceiling(src.Cols / (double)stepW));

        int paddedH = tilesY * stepH + 2 * overlap;
        int paddedW = tilesX * stepW + 2 * overlap;

        using Mat padded = new();
        Cv2.CopyMakeBorder(src, padded,
            overlap, paddedH - src.Rows - overlap,
            overlap, paddedW - src.Cols - overlap,
            BorderTypes.Replicate);

        Mat output = new(src.Rows * SrScaleInt, src.Cols * SrScaleInt, MatType.CV_8UC3);

        for (int ty = 0; ty < tilesY; ty++) {
            int coreY0 = ty * stepH;
            int coreH  = Math.Min(stepH, src.Rows - coreY0);

            for (int tx = 0; tx < tilesX; tx++) {
                int coreX0 = tx * stepW;
                int coreW  = Math.Min(stepW, src.Cols - coreX0);

                using Mat tile = padded.SubMat(new Rect(tx * stepW, ty * stepH, tileW, tileH));
                using Mat tileOut = RunSingleTile(session, tile);

                using Mat core = tileOut.SubMat(new Rect(
                    overlap * SrScaleInt, overlap * SrScaleInt, coreW * SrScaleInt, coreH * SrScaleInt));
                core.CopyTo(output[new Rect(
                    coreX0 * SrScaleInt, coreY0 * SrScaleInt, coreW * SrScaleInt, coreH * SrScaleInt)]);
            }
        }

        return output;
    }

    /// <summary>Runs one fixed-size tile through the ONNX session and returns the BGR uint8 result.</summary>
    private static Mat RunSingleTile( InferenceSession session, Mat tileBgrUint8 ) {
        DenseTensor<float> inputTensor = BuildInputTensor(tileBgrUint8);
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(TensorInput, inputTensor) };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = session.Run(inputs, [TensorOutput]);
        Tensor<float> outputTensor = outputs.First(o => o.Name == TensorOutput).AsTensor<float>();
        return TensorToMat(outputTensor);
    }

    /// <summary>BGR uint8 Mat -> NCHW float32 [1,3,H,W] normalized to [0,1], BGR channel order.</summary>
    private static DenseTensor<float> BuildInputTensor( Mat bgrUint8 ) {
        int h = bgrUint8.Rows, w = bgrUint8.Cols;
        var tensor = new DenseTensor<float>([1, 3, h, w]);
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                Vec3b px = bgrUint8.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = px.Item0 / 255f;  // B
                tensor[0, 1, y, x] = px.Item1 / 255f;  // G
                tensor[0, 2, y, x] = px.Item2 / 255f;  // R
            }
        }
        return tensor;
    }

    /// <summary>NCHW float32 output tensor [1,3,H,W] in [0,1] -> BGR uint8 Mat.</summary>
    private static Mat TensorToMat( Tensor<float> t ) {
        int h = t.Dimensions[2], w = t.Dimensions[3];
        Mat outBgr = new(h, w, MatType.CV_8UC3);
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                byte b = (byte)(Math.Clamp(t[0, 0, y, x], 0f, 1f) * 255f);
                byte g = (byte)(Math.Clamp(t[0, 1, y, x], 0f, 1f) * 255f);
                byte r = (byte)(Math.Clamp(t[0, 2, y, x], 0f, 1f) * 255f);
                outBgr.At<Vec3b>(y, x) = new Vec3b(b, g, r);
            }
        }
        return outBgr;
    }

    /// <summary>Resizes the x2 result to the exact target scale (relative to the original) via Lanczos4.</summary>
    private static Mat ApplyLanczos4( Mat original, Mat sr, double scaleFactor ) {
        int newW = (int)Math.Round(original.Cols * scaleFactor);
        int newH = (int)Math.Round(original.Rows * scaleFactor);
        if (newW == sr.Cols && newH == sr.Rows) return sr.Clone();
        Mat dst = new();
        Cv2.Resize(sr, dst, new OpenCvSharp.Size(newW, newH), interpolation: InterpolationFlags.Lanczos4);
        return dst;
    }
}
