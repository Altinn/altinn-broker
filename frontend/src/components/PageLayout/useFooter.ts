import type { FooterProps } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { useLanguage } from '../../i18n/useLanguage'
import {
  getAboutAltinnLink,
  getAccessibilityLink,
  getAnnouncementsLink,
  getNeedHelpLink,
  getPrivacyLink,
} from './altinnLinks'

export function useFooter(): FooterProps {
  const { language } = useLanguage()
  const { t } = useTranslation()

  return {
    address: t('footer.address'),
    address2: t('footer.address2'),
    menu: {
      items: [
        { id: 'help', href: getNeedHelpLink(language), title: t('footer.help') },
        { id: 'about', href: getAboutAltinnLink(language), title: t('footer.about') },
        { id: 'announcements', href: getAnnouncementsLink(language), title: t('footer.announcements') },
        { id: 'privacy', href: getPrivacyLink(language), title: t('footer.privacy') },
        { id: 'accessibility', href: getAccessibilityLink(language), title: t('footer.accessibility') },
      ],
    },
  }
}
