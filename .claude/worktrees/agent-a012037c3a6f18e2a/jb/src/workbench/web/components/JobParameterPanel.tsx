import type { PrismOutputFormat, PrismProcessingParameters } from "../services/prismApiClient";

interface JobParameterPanelProps {
  parameters: PrismProcessingParameters;
  onParametersChanged: (parameters: PrismProcessingParameters) => void;
}

const binaryParameterFields = [
  {
    key: "rename",
    label: "Rename",
    detail: "request.rename"
  },
  {
    key: "transform",
    label: "Transform",
    detail: "request.transform"
  },
  {
    key: "generation",
    label: "Generation",
    detail: "request.generation"
  },
  {
    key: "ReturnOriginalImages",
    label: "Return originals",
    detail: "request.ReturnOriginalImages"
  }
] as const;

export function JobParameterPanel({
  parameters,
  onParametersChanged
}: JobParameterPanelProps) {
  return (
    <section className="section-panel" aria-labelledby="parameters-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">One job-parameter location</p>
          <h2 id="parameters-heading">PrismProcessingParameters</h2>
        </div>
      </div>

      <div className="parameter-layout">
        <div>
          <label className="field-label" htmlFor="output-format">
            Output format
          </label>
          <select
            id="output-format"
            className="select-input"
            value={parameters.format}
            onChange={(event) =>
              onParametersChanged({
                ...parameters,
                format: event.currentTarget.value as PrismOutputFormat
              })
            }
          >
            <option value="zip">zip</option>
            <option value="json">json</option>
          </select>
        </div>

        <fieldset className="binary-parameter-group">
          <legend>Binary parameters</legend>
          {binaryParameterFields.map((field) => (
            <label className="toggle-row" key={field.key}>
              <input
                type="checkbox"
                checked={parameters[field.key]}
                onChange={(event) =>
                  onParametersChanged({
                    ...parameters,
                    [field.key]: event.currentTarget.checked
                  })
                }
              />
              <span>
                <strong>{field.label}</strong>
                <small>{field.detail}</small>
              </span>
            </label>
          ))}
        </fieldset>
      </div>
    </section>
  );
}
