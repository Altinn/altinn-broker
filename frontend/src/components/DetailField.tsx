import { ListItem } from '@altinn/altinn-components'
import './DetailField.css'

type DetailFieldProps = {
  label: string
  value: string | null | undefined
}

export function DetailField({ label, value }: DetailFieldProps) {
  return (
    <ListItem
      className="data-list-item"
      shadow="none"
      interactive={false}
      title={label}
      description={value}
    />
  )
}
