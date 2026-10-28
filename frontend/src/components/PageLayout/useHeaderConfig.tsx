import type { GlobalHeaderProps } from '@altinn/altinn-components'
import { useAccountSelector } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useLanguage } from '../../i18n/useLanguage'
import { useParties } from '../../parties/PartiesContext'
import { getFrontPageLink } from './altinnLinks'
import { useSidebarMenu } from './useSidebarMenu'

export function useHeaderConfig(): GlobalHeaderProps {
  const { desktopMenu, mobileMenu } = useSidebarMenu()
  const { logout } = useAuth()
  const { status, parties, selfPartyUuid, selectedParty, selectParty } = useParties()
  const { language, selectLanguage } = useLanguage()
  const { t } = useTranslation()

  // Without a currentAccount, GlobalHeader renders a dead "Logg inn" button (onLoginClick
  // is unset). Fall back to the self account when no organization is selected yet.
  const accountSelector = useAccountSelector({
    partyListDTO: parties,
    currentAccountUuid: selectedParty?.partyUuid ?? selfPartyUuid,
    selfAccountUuid: selfPartyUuid,
    isLoading: status === 'loading',
    virtualized: parties.length > 20,
    onSelectAccount: selectParty,
    languageCode: language,
  })

  return {
    logo: {
      as: (props) => <Link {...props} to={getFrontPageLink(language)} />,
    },
    locale: {
      title: t('locale.title'),
      backLabel: t('word.back'),
      options: [
        { label: t('locale.nb'), value: 'nb', checked: language === 'nb' },
        { label: t('locale.nn'), value: 'nn', checked: language === 'nn' },
        { label: t('locale.en'), value: 'en', checked: language === 'en' },
      ],
      onSelect: (value) => {
        void selectLanguage(value)
      },
    },
    accountSelector,
    globalMenu: {
      menuLabel: t('word.menu'),
      backLabel: t('word.back'),
      menu: desktopMenu,
      logoutButton: {
        label: t('word.log_out'),
        onClick: () => logout('/'),
      },
    },
    desktopMenu,
    mobileMenu,
  }
}
