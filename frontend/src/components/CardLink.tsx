import { Link } from 'react-router-dom'
import './CardLink.css'

type CardLinkProps = {
  title: string
  description?: string
  to: string
  avatarLetter?: string
}

export function CardLink({ title, description, to, avatarLetter }: CardLinkProps) {
  return (
    <Link to={to} className="card-link">
      {avatarLetter && (
        <span className="card-link__avatar" aria-hidden="true">
          {avatarLetter}
        </span>
      )}
      <span className="card-link__content">
        <span className="card-link__title">{title}</span>
        {description && <span className="card-link__description">{description}</span>}
      </span>
      <span className="card-link__chevron" aria-hidden="true">
        ›
      </span>
    </Link>
  )
}
