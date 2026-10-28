import { DialogLayout } from '@altinn/altinn-components'
import {
  Alert,
  Button,
  ErrorSummary,
  Heading,
  Paragraph,
  // Field,
  Spinner,
  // Switch,
  Textfield,
  Checkbox,
} from '@digdir/designsystemet-react'
import { useEffect, useRef, useState } from 'react'
import { Link, type LinkProps, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { BlockingUploadNotice } from '../components/NewFileTransferPage/BlockingUploadNotice'
// import { ChecksumFields } from '../components/NewFileTransferPage/ChecksumFields'
import { CancelUploadConfirmation } from '../components/NewFileTransferPage/CancelUploadConfirmation'
import { InterruptedUploadNotice } from '../components/NewFileTransferPage/InterruptedUploadNotice'
import { CustomRecipientsFields } from '../components/NewFileTransferPage/CustomRecipientsFields'
import { MetadataFields } from '../components/NewFileTransferPage/MetadataFields'
import { NotificationFields } from '../components/NewFileTransferPage/NotificationFields'
import { PartyField } from '../components/NewFileTransferPage/PartyField'
import { RecipientsField } from '../components/NewFileTransferPage/RecipientsField'
import { UploadFile } from '../components/NewFileTransferPage/UploadFile'
import { UploadProgress } from '../components/NewFileTransferPage/UploadProgress'
import { VirusScanField } from '../components/NewFileTransferPage/VirusScanField'
import {
  fieldId,
  fieldLabel,
  newFileTransferFields,
  type NewFileTransferField,
} from '../components/NewFileTransferPage/formFields'
import { MAX_REFERENCE_LENGTH } from '../components/NewFileTransferPage/formValidation'
import { useNewFileTransferForm } from '../components/NewFileTransferPage/useNewFileTransferForm'
import '../components/NewFileTransferPage/newFileTransferPage.css'
import { NoRecipientsNotice } from '../components/FileTransferServiceDetailPage/NoRecipientsNotice'
import { useFileTransferService } from '../components/FileTransferServiceDetailPage/useFileTransferService'
import { useParties } from '../parties/PartiesContext'
import { flattenParties } from '../parties/mapAuthorizedParty'
import { useUploadActions } from '../upload/uploadsContext'
import { useResumableUpload } from '../components/NewFileTransferPage/useResumableUpload'
import { PageRoutes, RESUME_PARAM, activeTransferPath, newFileTransferPath, servicePath } from './routes'

export function NewFileTransferPage() {
  const { serviceId = '' } = useParams()
  const [searchParams] = useSearchParams()
  const { selectedParty } = useParties()
  const resumeTransferId = searchParams.get(RESUME_PARAM)
  return (
    <NewFileTransferPageContent
      key={`${selectedParty?.organizationNumber ?? ''}:${serviceId}:${resumeTransferId ?? ''}`}
      resumeTransferId={resumeTransferId}
    />
  )
}

/** With a transfer whose upload stopped, the form continues that upload instead of sending a new one. */
function NewFileTransferPageContent({ resumeTransferId }: { resumeTransferId: string | null }) {

  const { t } = useTranslation()
  const { serviceId = '' } = useParams()
  const navigate = useNavigate()
  const { status: partiesStatus, parties, selectedParty, selectParty } = useParties()
  const senderOrgNumber = selectedParty?.organizationNumber ?? ''
  const serviceState = useFileTransferService(serviceId, selectedParty?.organizationNumber)
  const errorSummaryRef = useRef<HTMLDivElement>(null)
  const [confirmingCancel, setConfirmingCancel] = useState(false)

  const { addUploadSuccessListener } = useUploadActions()
  const resumable = useResumableUpload(resumeTransferId, serviceId, selectedParty)
  const resume = resumable.status === 'ready' ? resumable.upload : null
  const form = useNewFileTransferForm({ resourceId: serviceId, senderOrgNumber, resume })

  useEffect(
    () =>
      addUploadSuccessListener(({ fileTransferId, resourceId, sender }) => {
        if (resourceId === serviceId && sender === senderOrgNumber) {
          navigate(activeTransferPath(fileTransferId), { replace: true })
        }
      }),
    [navigate, senderOrgNumber, serviceId, addUploadSuccessListener],
  )

  // The upload is only shown as the form's own while acting for the organisation that sends it.
  const goToUpload = (upload: { resourceId: string; sender: string }) => {
    const owner = flattenParties(parties).find((party) => party.organizationNumber === upload.sender)
    if (owner && upload.sender !== senderOrgNumber) {
      selectParty(owner.partyUuid)
    }
    navigate(newFileTransferPath(upload.resourceId))
  }
  const { errors, setValue, submitAttempts, values } = form

  useEffect(() => {
    if (submitAttempts > 0) {
      errorSummaryRef.current?.focus()
    }
  }, [submitAttempts])

  if (partiesStatus === 'failed') {
    return <p>{t('page.parties.load_failed_short')}</p>
  }

  if (partiesStatus === 'loaded' && !selectedParty) {
    return <p>{t('page.parties.none_message')}</p>
  }

  if (serviceState === null) {
    return <Spinner aria-label={t('page.service.loading')} />
  }

  if (serviceState.status === 'failed') {
    return <p>{t('page.service.load_failed_short')}</p>
  }

  if (serviceState.status === 'missing') {
    return <p>{t('page.service.missing_short')}</p>
  }

  if (resumable.status === 'missing' || resumable.status === 'failed') {
    return (
      <div className="page">
        <Alert data-color="warning">
          {resumable.status === 'missing'
            ? 'Opplastingen kan ikke fortsettes. Filen ble ikke lastet opp ferdig, så formidlingen må sendes på nytt.'
            : 'Klarte ikke å hente opplastingen. Prøv igjen senere.'}
        </Alert>
        <p>
          <Link to={PageRoutes.drafts}>Tilbake til utkastene</Link>
        </p>
      </div>
    )
  }

  const service = serviceState.service.resource

  // Reachable by url even when the service detail page withholds the action.
  if (serviceState.service.allowedRecipients.length === 0) {
    return (
      <div className="page">
        <NoRecipientsNotice />
      </div>
    )
  }

  const failedFields = newFileTransferFields.filter((field) => errors[field])

  const { active, blockedBy } = form
  const status = active?.status ?? null

  // An upload that can be carried on takes the primary action over, so there is only one to press.
  // An interrupted one waits while another upload holds the place.
  const continueUpload =
    status === 'paused' || status === 'failed'
      ? form.resume
      : form.resumeReady && !blockedBy
        ? form.resumeInterrupted
        : null

  // The transfer settled these when it was sent, so they are shown but not editable.
  const detailsLocked = resume !== null

  const cancellable = (active !== null && active.status !== 'finishing') || form.interrupted !== null

  // A cancelled upload can't be continued, so the form goes back to sending a new transfer.
  const cancelUpload = () => {
    if (active) {
      form.cancel()
    } else {
      form.discardInterrupted()
    }
    if (resumeTransferId) {
      navigate(newFileTransferPath(serviceId), { replace: true })
    }
  }
  if (confirmingCancel && !cancellable) {
    setConfirmingCancel(false)
  }

  // The metadata summary entry points at the row input that failed instead of the entire metadata field.
  const metadataErrorTargetId = (field: NewFileTransferField) =>
    (field === 'metadata' ? form.metadataErrorInputId : undefined) ??
    (field === 'customRecipients' ? form.customRecipientErrorInputId : undefined) ??
    fieldId(field)

  return (
    <DialogLayout
      color="company"
      backButton={{
        label: t('page.common.back'),
        as: (props: LinkProps) => <Link {...props} to={servicePath(service.resourceId)} />,
      }}
    >
      <header className="new-transfer__header">
        <Heading level={2} data-size="md">
          {resumeTransferId ? 'Fortsett formidling' : 'Ny formidling'}
          {t('page.new.title')}
        </Heading>
        <Paragraph data-size="sm">{service.name ?? service.resourceId}</Paragraph>
      </header>

      {form.loading || resumable.status === 'loading' ? (
        <Spinner aria-label={t('page.service.loading_settings')} />
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

          {blockedBy && (
            <BlockingUploadNotice
              upload={blockedBy}
              onContinue={() => goToUpload(blockedBy)}
              onCancel={form.cancelBlocking}
            />
          )}

          <fieldset className="new-transfer__fields" disabled={active !== null}>
            <fieldset className="new-transfer__fields" disabled={detailsLocked}>
              <PartyField
                label={t('page.common.sender')}
                description={t('page.new.sender_description')}
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
                label={t('page.common.reference')}
                description={t('page.new.reference_description')}
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
              <Checkbox
                id={fieldId('notifications')}
                label={t('page.new.notify')}
                checked={values.notifications}
                onChange={(event) => setValue('notifications', event.target.checked)}
              />
              {values.notifications && (
                <>
                  <NotificationFields
                    sendReminder={values.sendReminder}
                    onSendReminderChange={(sendReminder) => setValue('sendReminder', sendReminder)}
                  />
                  <CustomRecipientsFields
                    id={fieldId('customRecipients')}
                    enabled={values.customRecipientsEnabled}
                    entries={values.customRecipients}
                    rowErrors={form.customRecipientRowErrors}
                    error={errors.customRecipients}
                    relatedOrganizations={values.recipients.map((organizationNumber) => ({
                      organizationNumber,
                      name: form.rules.options.find((option) => option.organizationNumber === organizationNumber)?.name,
                    }))}
                    onEnabledChange={(enabled) => setValue('customRecipientsEnabled', enabled)}
                    onChange={(customRecipients) => setValue('customRecipients', customRecipients)}
                  />
                </>
              )}
            </fieldset>

            {form.interrupted && (
              <InterruptedUploadNotice
                fileName={form.interrupted.fileName}
                fileSize={form.interrupted.fileSize}
                uploaded={form.interrupted.plan.uploadedBytes}
                ready={form.resumeReady}
              />
            )}

            <UploadFile
              id={fieldId('file')}
              file={values.file}
              maxFileSize={form.maxFileSize}
              error={form.wrongFile ?? errors.file}
              onChange={(file) => setValue('file', file)}
            />
            {form.checkingFile && <Paragraph data-size="sm">Sjekker at det er den samme filen …</Paragraph>}

            <fieldset className="new-transfer__fields" disabled={detailsLocked}>
              {/* Checksum form hidden for now; restore by uncommenting.
              <Field>
                <Switch
                  id={fieldId('checksumEnabled')}
                  label={t('page.new.validate_checksum')}
                  checked={values.checksumEnabled}
                  onChange={(event) => setValue('checksumEnabled', event.target.checked)}
                />
                <Field.Description>
                  {t('page.new.checksum_computed')}
                </Field.Description>
              </Field>
              {values.checksumEnabled && (
                <ChecksumFields
                  algorithm={values.checksumAlgorithm}
                  checksum={values.checksum}
                  error={errors.checksum}
                  onAlgorithmChange={(checksumAlgorithm) => setValue('checksumAlgorithm', checksumAlgorithm)}
                  onChecksumChange={(checksum) => setValue('checksum', checksum)}
                />
              )}
              */}
              <VirusScanField
                checked={values.virusScan}
                locked={form.virusScanLocked}
                onChange={(virusScan) => setValue('virusScan', virusScan)}
              />
            </fieldset>
          </fieldset>

          {failedFields.length > 0 && (
            <div ref={errorSummaryRef} tabIndex={-1} className="new-transfer__error-summary">
              <ErrorSummary>
                <ErrorSummary.Heading>{t('page.new.fix_errors')}</ErrorSummary.Heading>
                <ErrorSummary.List>
                  {failedFields.map((field) => (
                    <ErrorSummary.Item key={field}>
                      <ErrorSummary.Link href={`#${metadataErrorTargetId(field)}`}>
                        {fieldLabel(field)}: {errors[field]}
                      </ErrorSummary.Link>
                    </ErrorSummary.Item>
                  ))}
                </ErrorSummary.List>
              </ErrorSummary>
            </div>
          )}

          {form.submitError && <Alert data-color="danger">{form.submitError}</Alert>}

          {active && (
            <UploadProgress status={active.status} onPause={form.pause} onResume={form.resume} />
          )}

          {confirmingCancel ? (
            <CancelUploadConfirmation
              onConfirm={() => {
                setConfirmingCancel(false)
                cancelUpload()
              }}
              onDismiss={() => setConfirmingCancel(false)}
            />
          ) : (
            <div className="new-transfer__actions">
              {continueUpload || form.interrupted ? (
                <Button
                  type="button"
                  onClick={continueUpload ?? undefined}
                  disabled={continueUpload === null}
                >
                  {t('page.new.continue_upload')}
                </Button>
              ) : (
                <Button
                  type="submit"
                  loading={active !== null}
                  disabled={active !== null || blockedBy !== null}
                >
                  {active ? t('page.new.uploading') : t('page.new.send')}
                </Button>
              )}
              {cancellable && (
                <Button type="button" variant="secondary" onClick={() => setConfirmingCancel(true)}>
                  {t('page.new.cancel_upload')}
                </Button>
              )}
            </div>
          )}
        </form>
      )}
    </DialogLayout>
  )
}
