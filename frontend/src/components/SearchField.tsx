import { Search } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'

type SearchFieldProps = {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
}

export function SearchField({ id, label, value, onChange }: SearchFieldProps) {
  const { t } = useTranslation()
  return (
    <div className="field">
      <label className="page-subheading" htmlFor={id}>
        {label}
      </label>
      <Search>
        <Search.Input
          id={id}
          placeholder={t('page.common.search_placeholder')}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        />
        <Search.Clear onClick={() => onChange('')} />
      </Search>
    </div>
  )
}
