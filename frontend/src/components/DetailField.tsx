import './DetailField.css'

type DetailFieldProps = {
  label: string
  value: string
}

export function DetailField({ label, value }: DetailFieldProps) {
  return (
    <li className="detail-field">
      <span className="detail-field__label">{label}</span>
      <span className="detail-field__value">{value}</span>
    </li>
  )
}
