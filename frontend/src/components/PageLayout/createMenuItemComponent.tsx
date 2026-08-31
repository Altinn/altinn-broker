import type { ComponentProps } from 'react'
import { Link } from 'react-router-dom'

export const createMenuItemComponent =
  ({ to, isExternal = false }: { to: string; isExternal?: boolean }) =>
  (props: ComponentProps<'a'>) => {
    if (isExternal) {
      return <a {...props} href={to} target="_blank" rel="noreferrer" />
    }
    return <Link {...props} to={to} />
  }
