import { DialogLayout } from '@altinn/altinn-components'
import {
  Alert,
  Button,
  ErrorSummary,
  Heading,
  Paragraph,
  Spinner,
  Textfield,
} from '@digdir/designsystemet-react'
import { useCallback, useEffect, useRef } from 'react'
import { Link, type LinkProps, useNavigate, useParams } from 'react-router-dom'
import { toast } from 'react-toastify'
import { MetadataFields } from '../components/NewFileTransferPage/MetadataFields'
import { PartyField } from '../components/NewFileTransferPage/PartyField'
import { RecipientsField } from '../components/NewFileTransferPage/RecipientsField'
import { UploadFile } from '../components/NewFileTransferPage/UploadFile'
import { UploadProgress } from '../components/NewFileTransferPage/UploadProgress'
import { VirusScanField } from '../components/NewFileTransferPage/VirusScanField'
import {
  fieldId,
  fieldLabels,
  type NewFileTransferField,
} from '../components/NewFileTransferPage/formFields'
import { MAX_REFERENCE_LENGTH } from '../components/NewFileTransferPage/formValidation'
import { useNewFileTransferForm } from '../components/NewFileTransferPage/useNewFileTransferForm'
import '../components/NewFileTransferPage/newFileTransferPage.css'
import { useFileTransferService } from '../components/FileTransferServiceDetailPage/useFileTransferService'
import { useParties } from '../parties/PartiesContext'
import { activeTransferPath, servicePath } from './routes'

export function NewFileTransferPage() {
  const { serviceId = '' } = useParams()
  const navigate = useNavigate()
  const { status: partiesStatus, selectedParty } = useParties()
  const senderOrgNumber = selectedParty?.organizationNumber ?? ''
  const serviceState = useFileTransferService(serviceId, selectedParty?.organizationNumber)
  const errorSummaryRef = useRef<HTMLDivElement>(null)

  const onSent = useCallback(
    (fileTransferId: string) => {
      toast.success('Formidlingen er sendt, og filen er lastet opp.')
      navigate(activeTransferPath(fileTransferId), { replace: true })
    },
    [navigate],
  )

  const form = useNewFileTransferForm({ resourceId: serviceId, senderOrgNumber, onSent })
  const { errors, setValue, submitAttempts, values } = form

  useEffect(() => {
    if (submitAttempts > 0) {
      errorSummaryRef.current?.focus()
    }
  }, [submitAttempts])

  if (partiesStatus === 'failed') {
    return <p>Klarte ikke å hente aktører.</p>
  }

  if (partiesStatus === 'loaded' && !selectedParty) {
    return <p>Du kan ikke representere noen virksomheter i BrokerBox.</p>
  }

  if (serviceState === null) {
    return <Spinner aria-label="Henter formidlingstjenesten" />
  }

  if (serviceState.status === 'failed') {
    return <p>Klarte ikke å hente formidlingstjenesten.</p>
  }

  if (serviceState.status === 'missing') {
    return <p>Fant ikke formidlingstjenesten.</p>
  }

  const service = serviceState.service.resource

  const cancel = () => {
    form.abort()
    navigate(servicePath(service.resourceId))
  }

  const failedFields = (Object.keys(fieldLabels) as NewFileTransferField[]).filter(
    (field) => errors[field],
  )

  // The metadata summary entry points at the row input that failed instead of the entire metadata field.
  const metadataErrorTargetId = (field: NewFileTransferField) =>
    (field === 'metadata' ? form.metadataErrorInputId : undefined) ?? fieldId(field)

  return (
    <DialogLayout
      color="company"
      backButton={{
        label: 'Tilbake',
        as: (props: LinkProps) => <Link {...props} to={servicePath(service.resourceId)} />,
      }}
    >
      <header className="new-transfer__header">
        <Heading level={2} data-size="md">
          Ny formidling
        </Heading>
        <Paragraph data-size="sm">{service.name ?? service.resourceId}</Paragraph>
      </header>

      {form.loading ? (
        <Spinner aria-label="Henter oppsettet for tjenesten" />
      ) : (
        <form
          className="new-transfer__form"
          noValidate
          onSubmit={(event) => {
            event.preventDefault()
            void form.submit()
          }}
        >
          {form.loadError && <Alert data-color="warning">{form.loadError}</Alert>}

          <fieldset className="new-transfer__fields" disabled={form.sending}>
            <PartyField
              label="Avsender"
              description="Du formidler på vegne av denne organisasjonen."
              name={selectedParty?.name ?? ''}
              organizationNumber={senderOrgNumber}
            />

            <RecipientsField
              id={fieldId('recipients')}
              rules={form.rules}
              selected={values.recipients}
              error={errors.recipients}
              onChange={(recipients) => setValue('recipients', recipients)}
            />

            <Textfield
              id={fieldId('reference')}
              label="Referanse"
              description="Din egen referanse til formidlingen, slik at du kan kjenne den igjen senere."
              value={values.reference}
              error={errors.reference}
              maxLength={MAX_REFERENCE_LENGTH}
              onChange={(event) => setValue('reference', event.target.value)}
            />

            <MetadataFields
              id={fieldId('metadata')}
              entries={values.metadata}
              rowErrors={form.metadataRowErrors}
              onChange={(metadata) => setValue('metadata', metadata)}
            />

            <UploadFile
              id={fieldId('file')}
              file={values.file}
              maxFileSize={form.maxFileSize}
              error={errors.file}
              onChange={(file) => setValue('file', file)}
            />

            <VirusScanField
              checked={values.virusScan}
              locked={form.virusScanLocked}
              onChange={(virusScan) => setValue('virusScan', virusScan)}
            />
          </fieldset>

          {failedFields.length > 0 && (
            <div ref={errorSummaryRef} tabIndex={-1} className="new-transfer__error-summary">
              <ErrorSummary>
                <ErrorSummary.Heading>Rett opp disse før du sender</ErrorSummary.Heading>
                <ErrorSummary.List>
                  {failedFields.map((field) => (
                    <ErrorSummary.Item key={field}>
                      <ErrorSummary.Link href={`#${metadataErrorTargetId(field)}`}>
                        {fieldLabels[field]}: {errors[field]}
                      </ErrorSummary.Link>
                    </ErrorSummary.Item>
                  ))}
                </ErrorSummary.List>
              </ErrorSummary>
            </div>
          )}

          {form.submitError && <Alert data-color="danger">{form.submitError}</Alert>}

          {form.sending && <UploadProgress progress={form.progress} />}

          <div className="new-transfer__actions">
            <Button type="submit" loading={form.sending}>
              {form.sending ? 'Laster opp…' : 'Send formidling'}
            </Button>
            <Button type="button" variant="secondary" onClick={cancel}>
              Avbryt
            </Button>
          </div>
        </form>
      )}
    </DialogLayout>
  )
}
