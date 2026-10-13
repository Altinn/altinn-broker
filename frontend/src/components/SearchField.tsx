import { Search } from '@digdir/designsystemet-react'

type SearchFieldProps = {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
}

export function SearchField({ id, label, value, onChange }: SearchFieldProps) {
  return (
    <div className="field">
      <label className="page-subheading" htmlFor={id}>
        {label}
      </label>
      <Search>
        <Search.Input
          id={id}
          placeholder="Søk..."
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
        <Search.Clear onClick={() => onChange('')} />
      </Search>
    </div>
  )
}
