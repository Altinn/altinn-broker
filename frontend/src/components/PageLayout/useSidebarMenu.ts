import type { MenuItemProps, MenuProps } from '@altinn/altinn-components'
import {
  ArchiveIcon,
  ArrowRightLeftIcon,
  BellIcon,
  Buildings2Icon,
  ChatExclamationmarkIcon,
  ExternalLinkIcon,
  FileCheckmarkIcon,
  InboxFillIcon,
  HeartIcon,
  MagnifyingGlassIcon,
  MenuGridIcon,
  PadlockLockedFillIcon,
  PersonCircleIcon,
  PlusIcon,
} from '@navikt/aksel-icons'
import { useLocation } from 'react-router-dom'
import { claimValue } from '../../auth/types'
import { useAuth } from '../../auth/AuthContext'
import { useParties } from '../../parties/PartiesContext'
import { PageRoutes } from '../../pages/routes'
import {
  getAccessManagementLink,
  getArbeidsflateLink,
  getFrontPageLink,
  getHelpPagesLink,
  getNeedHelpLink,
  getNewFormLink,
  getStartNewBusinessLink,
} from './altinnLinks'
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

export function useSidebarMenu(): { sidebarMenu: MenuProps; desktopMenu: MenuProps; mobileMenu: MenuProps } {
  const { pathname } = useLocation()
  const { user } = useAuth()
  const { parties } = useParties()
  const endUserName = (user && claimValue(user, 'name')) || 'Deg'
  const hasOnlySelfParty = parties.every((party) => party.type === 'Person')

  const menuGroups: MenuProps['groups'] = {
    shortcuts: {
      divider: true,
      size: 'sm',
      title: 'Snarveier',
    },
    global: {
      divider: false,
    },
    help: {
      divider: true,
      size: 'sm',
    },
    'profile-shortcut': {
      divider: true,
      title: `Logget inn som ${endUserName}`,
    },
  }

  const inboxItem: MenuItemProps = {
    id: 'inbox',
    groupId: 'global',
    size: 'lg',
    icon: InboxFillIcon,
    title: 'Innboks',
    selected: false,
    as: createMenuItemComponent({ to: getArbeidsflateLink('/') }),
  }

  const fileTransferItems: MenuItemProps[] = [
    {
      id: 'file-transfers',
      groupId: 'global',
      size: 'lg',
      icon: ArrowRightLeftIcon,
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

  const shortcuts: MenuItemProps[] = [
    {
      id: 'new-schema',
      groupId: 'shortcuts',
      icon: PlusIcon,
      title: 'Nytt skjema',
      as: createMenuItemComponent({ to: getNewFormLink() }),
    },
    {
      id: 'profile',
      groupId: 'shortcuts-profile',
      icon: PersonCircleIcon,
      title: 'Din profil',
      as: createMenuItemComponent({ to: getArbeidsflateLink('/profile') }),
    },
    {
      id: 'saved-searches',
      groupId: 'shortcuts-profile',
      icon: MagnifyingGlassIcon,
      title: 'Lagrede søk',
      as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/saved-searches') }),
    },
  ]

  const helpItems: MenuItemProps[] = [
    {
      id: 'start-business',
      groupId: 'help',
      icon: Buildings2Icon,
      title: 'Starte og drive bedrift',
      as: createMenuItemComponent({ to: getStartNewBusinessLink() }),
    },
    {
      id: 'need-help',
      groupId: 'help',
      icon: ChatExclamationmarkIcon,
      title: 'Trenger du hjelp?',
      as: createMenuItemComponent({ to: getNeedHelpLink() }),
    },
  ]

  const helpPages: MenuItemProps = {
    id: 'help-pages',
    groupId: 'help',
    icon: ExternalLinkIcon,
    title: 'Hjelpesider',
    as: createMenuItemComponent({ to: getHelpPagesLink(), isExternal: true }),
  }

  const profileShortcutItems: MenuItemProps[] = [
    ...(hasOnlySelfParty
      ? []
      : [
          {
            id: 'parties',
            groupId: 'profile-children',
            icon: HeartIcon,
            size: 'sm' as const,
            title: 'Aktører og favoritter',
            as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/parties') }),
          },
          {
            id: 'notifications',
            groupId: 'profile-children',
            icon: BellIcon,
            size: 'sm' as const,
            title: 'Varslingsadresser',
            as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/notifications') }),
          },
        ]),
    {
      id: 'saved-searches-profile',
      groupId: 'saved-searches',
      icon: MagnifyingGlassIcon,
      size: 'sm',
      title: 'Lagrede søk',
      as: createMenuItemComponent({ to: getArbeidsflateLink('/profile/saved-searches') }),
    },
  ]

  const sidebarMenu: MenuProps = {
    variant: 'tinted',
    groups: menuGroups,
    items: [inboxItem, ...fileTransferItems, ...shortcuts, helpPages],
  }

  const mobileMenu: MenuProps = {
    groups: menuGroups,
    items: [
      inboxItem,
      ...fileTransferItems,
      {
        id: 'am',
        groupId: 'global',
        size: 'lg',
        icon: PadlockLockedFillIcon,
        as: 'a',
        href: getAccessManagementLink(),
        title: 'Tilgangsstyring',
        selected: false,
      },
      {
        id: 'all-forms',
        groupId: 'global',
        size: 'lg',
        icon: MenuGridIcon,
        title: 'Alle skjema og tjenester',
        as: createMenuItemComponent({ to: getNewFormLink() }),
        selected: false,
      },
      {
        id: 'altinn-search',
        groupId: 'global',
        size: 'lg',
        icon: MagnifyingGlassIcon,
        title: 'Søk i Altinn',
        as: createMenuItemComponent({ to: `${getFrontPageLink()}/sok?` }),
        selected: false,
      },
      ...helpItems,
      helpPages,
      {
        groupId: 'profile-shortcut',
        id: 'profile',
        size: 'sm',
        as: createMenuItemComponent({ to: getArbeidsflateLink('/profile') }),
        icon: PersonCircleIcon,
        title: 'Din profil',
        selected: false,
        expanded: true,
        items: profileShortcutItems,
      },
    ],
  }

  // Desktop header menu keeps Innboks first, and shows Formidlinger as one selected row.
  const desktopMenu: MenuProps = {
    ...mobileMenu,
    items: [
      inboxItem,
      { ...fileTransferItems[0], expanded: false, items: [], selected: true },
      ...mobileMenu.items.slice(2),
    ],
  }

  return { sidebarMenu, desktopMenu, mobileMenu }
}
