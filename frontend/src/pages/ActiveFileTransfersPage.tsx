import { useMemo, useState } from 'react'
import { activeTransfers, fileTransferServices } from '../data/mockData'
import { TransferListItem } from '../components/TransferListItem'
import { activeTransferPath } from './routes'
import './pages.css'

export function ActiveFileTransfersPage() {
  const [referenceSearch, setReferenceSearch] = useState('')
  const [serviceFilter, setServiceFilter] = useState('')

  const filtered = useMemo(() => {
    return activeTransfers.filter((transfer) => {
      const matchesReference =
        !referenceSearch ||
        transfer.reference.toLowerCase().includes(referenceSearch.toLowerCase()) ||
        transfer.subtitle.toLowerCase().includes(referenceSearch.toLowerCase())
      const matchesService = !serviceFilter || transfer.serviceId === serviceFilter
      return matchesReference && matchesService
    })
  }, [referenceSearch, serviceFilter])

  return (
    <div className="page">
      <h2 className="page-heading">Aktive formidlinger Brønnøy sykehus er delaktig i</h2>

      <div className="filter-row">
        <div className="field">
          <label className="label" htmlFor="ref-search">
            Søk på Referanse
          </label>
          <input
            id="ref-search"
            type="search"
            className="input"
            placeholder="Søk..."
            value={referenceSearch}
            onChange={(e) => setReferenceSearch(e.target.value)}
          />
        </div>
        <div className="field">
          <label className="label" htmlFor="service-filter">
            Filtrer på tjeneste
          </label>
          <select
            id="service-filter"
            className="input"
            value={serviceFilter}
            onChange={(e) => setServiceFilter(e.target.value)}
          >
            <option value="">Alle tjenester</option>
            {fileTransferServices.map((service) => (
              <option key={service.id} value={service.id}>
                {service.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <ul className="transfer-list">
        {filtered.map((transfer) => (
          <TransferListItem
            key={transfer.id}
            serviceName={transfer.serviceName}
            subtitle={transfer.subtitle}
            to={activeTransferPath(transfer.id)}
          />
        ))}
      </ul>

      {filtered.length === 0 && <p className="empty-state">Ingen aktive formidlinger funnet.</p>}
    </div>
  )
}
