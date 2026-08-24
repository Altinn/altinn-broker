import type { GlobalHeaderProps } from '@altinn/altinn-components'
import { useAccountSelector } from '@altinn/altinn-components'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { PageRoutes } from '../../pages/routes'
import { mockAuthorizedParties, mockCurrentAccountUuid } from './mockParties'
import { useSidebarMenu } from './useSidebarMenu'

export function useHeaderConfig(): GlobalHeaderProps {
  const sidebarMenu = useSidebarMenu()
  const { logout } = useAuth()

  const accountSelector = useAccountSelector({
    partyListDTO: mockAuthorizedParties,
    currentAccountUuid: mockCurrentAccountUuid,
    favoriteAccountUuids: [],
    isLoading: false,
    languageCode: 'nb',
  })

  return {
    logo: {
      as: (props) => <Link {...props} to={PageRoutes.fileTransfers} />,
    },
    locale: {
      title: 'Språk/language',
      options: [
        { label: 'Bokmål', value: 'nb', checked: true },
        { label: 'Nynorsk', value: 'nn', checked: false },
        { label: 'English', value: 'en', checked: false },
      ],
      onSelect: () => {},
    },
    accountSelector,
    globalMenu: {
      menuLabel: 'Meny',
      backLabel: 'Tilbake',
      menu: sidebarMenu,
      logoutButton: {
        label: 'Logg ut',
        onClick: () => logout('/'),
      },
    },
    desktopMenu: sidebarMenu,
  }
}
