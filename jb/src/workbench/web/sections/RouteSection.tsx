import { StageRouteList } from "../components/StageRouteList";
import type { PrismProgressEvent } from "../services/prismApiClient";

interface RouteSectionProps {
  events: PrismProgressEvent[];
}

export function RouteSection({ events }: RouteSectionProps) {
  return (
    <section className="section-panel" aria-labelledby="route-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Definitive route order</p>
          <h2 id="route-heading">
            Imported &gt; Classified &gt; Matched &gt; Ordered &gt; Renamed &gt; Generated &gt;
            Transformed &gt; Exported
          </h2>
        </div>
      </div>

      <StageRouteList events={events} />
    </section>
  );
}
