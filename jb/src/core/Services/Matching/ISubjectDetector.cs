using OpenCvSharp;

namespace Prism.Services.Matching;

/// <summary>
/// Producer seam for subject isolation. Implementations run upstream (Classify/preprocessing) and emit
/// a <see cref="Prism.Contracts.SubjectDetectionResult"/> consumed by the Transformed stage. One contract,
/// swappable producer: the v1 classical-CV detector today; a segmentation model (yolo26s-seg / SAM3,
/// see T-2600) later — with no Transform-side change when the producer is swapped. ONNX-based producers
/// stay upstream so the transformations themselves remain deterministic.
/// </summary>
public interface ISubjectDetector {
    // bgrImage: the decoded BGR Mat that ImagePreProcessor already holds. The producer must not dispose
    // it — the caller owns its lifetime.
    SubjectDetectionResult Detect(Mat bgrImage);
}
