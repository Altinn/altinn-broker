import { Link } from 'react-router-dom'
import './TransferListItem.css'

type TransferListItemProps = {
  serviceName: string
  subtitle: string
  to: string
}

export function TransferListItem({ serviceName, subtitle, to }: TransferListItemProps) {
  return (
    <li className="transfer-list-item">
      <Link to={to} className="transfer-list-item__link">
        <div>
          <div className="transfer-list-item__title">{serviceName}</div>
          <div className="transfer-list-item__subtitle">{subtitle}</div>
        </div>
        <span className="transfer-list-item__chevron" aria-hidden="true">
          ›
        </span>
      </Link>
    </li>
  )
}
