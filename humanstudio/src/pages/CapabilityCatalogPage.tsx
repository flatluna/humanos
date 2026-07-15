import { capabilities } from '../data/capabilities'

export function CapabilityCatalogPage() {
  return (
    <div className="page capabilities-page">
      <h2>Capabilities Catalog</h2>
      <div className="capabilities-list">
        {capabilities.map(cap => (
          <div key={cap.id} className="capability-card">
            <h3>{cap.name}</h3>
            <p>{cap.description}</p>
            <div className="capability-meta">
              <span className="category">{cap.category}</span>
              <span className="domain">{cap.domain}</span>
              <span className="status">{cap.status}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
