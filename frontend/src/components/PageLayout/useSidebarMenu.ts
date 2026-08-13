import type { MenuItemProps, MenuProps } from '@altinn/altinn-components'
import {
  ArchiveIcon,
  ExternalLinkIcon,
  FileCheckmarkIcon,
  FileIcon,
  PersonCircleIcon,
} from '@navikt/aksel-icons'
import { useLocation } from 'react-router-dom'
import { PageRoutes } from '../../pages/routes'
import { createMenuItemComponent } from './createMenuItemComponent'

function isRouteSelected(currentRoute: string, targetRoute: string) {
  if (currentRoute === targetRoute) {
    return true
  }

  if (targetRoute === PageRoutes.fileTransfers && currentRoute.startsWith('/file-transfers')) {
    return currentRoute === PageRoutes.fileTransfers
  }

  if (targetRoute === PageRoutes.services && currentRoute.startsWith('/file-transfers/services')) {
    return true
  }

  if (targetRoute === PageRoutes.active && currentRoute.startsWith('/file-transfers/active')) {
    return true
  }

  if (targetRoute === PageRoutes.historical && currentRoute.startsWith('/file-transfers/historical')) {
    return true
  }

  return false
}

export function useSidebarMenu(): MenuProps {
  const { pathname } = useLocation()

  const fileTransferItems: MenuItemProps[] = [
    {
      id: 'file-transfers',
      groupId: 'global',
      size: 'lg',
      icon: FileIcon,
      title: 'Formidlinger',
      selected: isRouteSelected(pathname, PageRoutes.fileTransfers),
      expanded: true,
      as: createMenuItemComponent({ to: PageRoutes.fileTransfers }),
      items: [
        {
          id: 'services',
          size: 'md',
          groupId: 'file-transfers',
          icon: PersonCircleIcon,
          title: 'Dine formidlingstjenester',
          selected: isRouteSelected(pathname, PageRoutes.services),
          as: createMenuItemComponent({ to: PageRoutes.services }),
        },
        {
          id: 'active',
          size: 'md',
          groupId: 'file-transfers',
          icon: FileCheckmarkIcon,
          title: 'Aktive formidlinger',
          selected: isRouteSelected(pathname, PageRoutes.active),
          as: createMenuItemComponent({ to: PageRoutes.active }),
        },
        {
          id: 'historical',
          size: 'md',
          groupId: 'file-transfers',
          icon: ArchiveIcon,
          title: 'Historiske formidlinger',
          selected: isRouteSelected(pathname, PageRoutes.historical),
          as: createMenuItemComponent({ to: PageRoutes.historical }),
        },
      ],
    },
  ]

  return {
    variant: 'tinted',
    groups: {
      global: {
        divider: false,
      },
      help: {
        divider: true,
        size: 'sm',
      },
    },
    items: [
      ...fileTransferItems,
      {
        id: 'help-pages',
        groupId: 'help',
        icon: ExternalLinkIcon,
        title: 'Hjelpesider',
        as: createMenuItemComponent({ to: 'https://info.altinn.no/hjelp/', isExternal: true }),
      },
    ],
  }
}
