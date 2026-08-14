import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { currentOrganization, getServiceById, organizations } from '../data/mockData'
import { servicePath } from './routes'
import './pages.css'

export function NewFileTransferPage() {
  const { serviceId = '' } = useParams()
  const navigate = useNavigate()
  const service = getServiceById(serviceId)

  const [reference, setReference] = useState('2026/123987')
  const [metadata, setMetadata] = useState('')
  const [senderId, setSenderId] = useState('922194912')
  const [recipientId, setRecipientId] = useState('889640782')
  const [virusScan, setVirusScan] = useState(true)
  const [notifyEmail, setNotifyEmail] = useState(true)
  const [notifySms, setNotifySms] = useState(false)
  const [fileName, setFileName] = useState('')

  if (!service) {
    return <p>Fant ikke formidlingstjenesten.</p>
  }

  const sender = organizations.find((o) => o.orgNumber.replace(/\s/g, '') === senderId)
  const recipient = organizations.find((o) => o.orgNumber.replace(/\s/g, '') === recipientId)
  const displayFileName = fileName || 'formidling'

  const canSubmit = reference && senderId && recipientId && fileName

  return (
    <div className="page">
      <div className="page-actions">
        <Link to={servicePath(service.id)} className="button button--secondary">
          ← Avbryt ny formidling
        </Link>
      </div>

      <div className="form-card">
        <h2 className="page-heading">Ny formidling</h2>
        <p className="page-subheading">{service.name}</p>

        <form className="form-stack" onSubmit={(e) => e.preventDefault()}>
          <div className="field">
            <label className="label" htmlFor="referanse">
              Referanse
            </label>
            <input
              id="referanse"
              className="input"
              type="text"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
            />
          </div>

          <div className="field">
            <label className="label" htmlFor="metadata">
              Andre metadata
            </label>
            <input
              id="metadata"
              className="input"
              type="text"
              value={metadata}
              onChange={(e) => setMetadata(e.target.value)}
            />
          </div>

          <div className="field">
            <label className="label" htmlFor="avsender">
              Avsender
            </label>
            <select
              id="avsender"
              className="input"
              value={senderId}
              onChange={(e) => setSenderId(e.target.value)}
            >
              <option value="">Velg avsender</option>
              <option value="922194912">922 194 912 – Brønnøy sykehus</option>
              <option value="985616167">985 616 167 – Sandnessjøen sykehus</option>
            </select>
          </div>

          <div className="field">
            <label className="label" htmlFor="mottaker">
              Mottaker
            </label>
            <select
              id="mottaker"
              className="input"
              value={recipientId}
              onChange={(e) => setRecipientId(e.target.value)}
            >
              <option value="">Velg mottaker</option>
              <option value="985616167">985 616 167 – Sandnessjøen sykehus</option>
              <option value="985627706">985 627 706 – St. Olavs hospital</option>
              <option value="889640782">889 640 782 – Haukeland sykehus</option>
              <option value="985399077">985 399 077 – Lovisenberg diakonale sykehus</option>
            </select>
          </div>

          <div className="field field--inline">
            <input
              id="virusskanning"
              type="checkbox"
              checked={virusScan}
              onChange={(e) => setVirusScan(e.target.checked)}
            />
            <label className="label" htmlFor="virusskanning">
              Virusskanning
            </label>
          </div>

          <div className="field">
            <label className="label" htmlFor="fil">
              Fil
            </label>
            <input
              id="fil"
              className="input"
              type="file"
              onChange={(e) => setFileName(e.target.files?.[0]?.name ?? '')}
            />
          </div>

          <fieldset className="fieldset">
            <legend className="label">
              Send melding til mottaker når filen er klar for nedlasting
            </legend>
            <p className="help-text">Velg alle alternativene som er relevante for deg.</p>

            <div className="field field--inline">
              <input
                id="varsling-epost"
                type="checkbox"
                checked={notifyEmail}
                onChange={(e) => setNotifyEmail(e.target.checked)}
              />
              <label className="label" htmlFor="varsling-epost">
                E-post
              </label>
            </div>
            {notifyEmail && (
              <p className="notification-preview">
                Hei. {sender?.name ?? currentOrganization.name} ({sender?.orgNumber ?? currentOrganization.orgNumber}) har
                sendt en fil ({displayFileName}) med Referanse {reference} til{' '}
                {recipient?.name ?? 'Haukeland sykehus'} ({recipient?.orgNumber ?? '889 640 782'}) i Altinn på vegne av
                Helsedirektoratet. Logg inn på altinn.no, representer {recipient?.name ?? 'Haukeland sykehus'} og velg
                Meny → Formidling for å laste ned filen.
              </p>
            )}

            <div className="field field--inline">
              <input
                id="varsling-sms"
                type="checkbox"
                checked={notifySms}
                onChange={(e) => setNotifySms(e.target.checked)}
              />
              <label className="label" htmlFor="varsling-sms">
                SMS
              </label>
            </div>
            {notifySms && (
              <p className="notification-preview">
                Hei. {sender?.name ?? currentOrganization.name} har sendt en fil ({displayFileName}) til{' '}
                {recipient?.name ?? 'Haukeland sykehus'} i Altinn. Logg inn på altinn.no for å laste ned filen.
              </p>
            )}
          </fieldset>

          <div className="form-actions">
            <button type="button" className="button" disabled={!canSubmit}>
              Lagre og start opplasting
            </button>
            <button
              type="button"
              className="button button--secondary"
              onClick={() => navigate(servicePath(service.id))}
            >
              Avbryt
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
