export function DomainsPage() {
  const domains = [
    { id: 1, name: 'Language', description: 'Natural language processing' },
    { id: 2, name: 'Vision', description: 'Computer vision and image analysis' },
    { id: 3, name: 'Audio', description: 'Audio processing and speech' }
  ]

  return (
    <div className="page domains-page">
      <h2>Domains</h2>
      <div className="domains-list">
        {domains.map(domain => (
          <div key={domain.id} className="domain-card">
            <h3>{domain.name}</h3>
            <p>{domain.description}</p>
          </div>
        ))}
      </div>
    </div>
  )
}
