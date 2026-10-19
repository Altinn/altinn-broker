import { Fieldset, Radio, Textfield } from '@digdir/designsystemet-react'
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
  return (
    <Fieldset>
      <Fieldset.Legend>Sjekksum</Fieldset.Legend>
      <Fieldset.Description>Broker beregner sjekksummen når filen er lastet opp.</Fieldset.Description>

      <Fieldset>
        <Fieldset.Legend>Algoritme</Fieldset.Legend>
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
        label="Kontroller mot denne sjekksummen (valgfritt)"
        description="La feltet stå tomt hvis Broker bare skal beregne og lagre sjekksummen."
        value={checksum}
        error={error}
        spellCheck={false}
        autoComplete="off"
        onChange={(event) => onChecksumChange(event.target.value)}
      />
    </Fieldset>
  )
}
