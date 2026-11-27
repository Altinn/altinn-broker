import type { ComponentProps } from 'react'
import { Link } from 'react-router-dom'

/** Renders a menu row as a router link, including absolute Altinn URLs. */
export const createMenuItemComponent =
  ({ to, isExternal = false }: { to: string; isExternal?: boolean }) =>
  ({ href, onChange, ...props }: ComponentProps<'a'> & { onChange?: unknown }) => {
    void href
    void onChange
    return (
      <Link {...props} to={to} {...(isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {})} />
    )
  }
