import { Fieldset, Radio, Textfield } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import type { ChecksumAlgorithm } from '../../api/initializeFileTransfer'
import { fieldId } from './formFields'

type ChecksumFieldsProps = {
  algorithm: ChecksumAlgorithm
  checksum: string
  error?: string
  onAlgorithmChange: (algorithm: ChecksumAlgorithm) => void
  onChecksumChange: (checksum: string) => void
}

const ALGORITHMS: ChecksumAlgorithm[] = ['SHA256', 'MD5']

/**
 * The algorithm is always sent. The checksum value is optional: empty means Broker computes it
 * after upload, and a value means the transfer fails when the file does not match.
 */
export function ChecksumFields({
  algorithm,
  checksum,
  error,
  onAlgorithmChange,
  onChecksumChange,
}: ChecksumFieldsProps) {
  const { t } = useTranslation()
  return (
    <>
      <Fieldset>
        <Fieldset.Legend>{t('page.checksum.algorithm')}</Fieldset.Legend>
        <div className="new-transfer__checksum-algorithms">
          {ALGORITHMS.map((option) => (
            <Radio
              key={option}
              name="checksum-algorithm"
              label={option}
              value={option}
              checked={algorithm === option}
              onChange={() => onAlgorithmChange(option)}
            />
          ))}
        </div>
      </Fieldset>

      <Textfield
        id={fieldId('checksum')}
        label={t('page.checksum.label')}
        description={t('page.checksum.description')}
        value={checksum}
        error={error}
        spellCheck={false}
        autoComplete="off"
        onChange={(event) => onChecksumChange(event.target.value)}
      />
    </>
  )
}
