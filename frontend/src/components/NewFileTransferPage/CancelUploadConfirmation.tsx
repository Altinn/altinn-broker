import { Button, Paragraph } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'

type CancelUploadConfirmationProps = {
  onConfirm: () => void
  onDismiss: () => void
}

export function CancelUploadConfirmation({ onConfirm, onDismiss }: CancelUploadConfirmationProps) {
  const { t } = useTranslation()
  return (
    <>
      <Paragraph data-size="sm">{t('page.cancel.body')}</Paragraph>
      <div className="new-transfer__resume-actions">
        <Button type="button" variant="primary" data-color="danger" data-size="sm" onClick={onConfirm}>
          {t('page.cancel.confirm')}
        </Button>
        <Button type="button" variant="tertiary" data-size="sm" onClick={onDismiss}>
          {t('page.cancel.keep')}
        </Button>
      </div>
    </>
  )
}
