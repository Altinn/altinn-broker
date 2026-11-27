import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { fetchProfileLanguage, saveProfileLanguage } from '../api/profileLanguage'
import { useAuth } from '../auth/AuthContext'
import { i18n, isAppLanguage, type AppLanguage } from './config'
import { LanguageContext } from './useLanguage'

export function LanguageProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const [language, setLanguage] = useState<AppLanguage>('nb')
  const languageRef = useRef<AppLanguage>('nb')
  // Bumps when the user picks a language, so a profile response already in flight cannot overwrite that choice.
  const selection = useRef(0)

  const applyLanguage = useCallback(async (next: AppLanguage) => {
    languageRef.current = next
    setLanguage(next)
    await i18n.changeLanguage(next)
  }, [])

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }

    const requestSelection = selection.current
    let cancelled = false
    fetchProfileLanguage()
      .then((next) => {
        if (cancelled || requestSelection !== selection.current) {
          return
        }
        return applyLanguage(next)
      })
      .catch(() => {
        // Keep the current language when the profile cannot be read.
      })

    return () => {
      cancelled = true
    }
  }, [applyLanguage, isAuthenticated])

  const selectLanguage = useCallback(
    async (value: string) => {
      if (!isAppLanguage(value) || value === languageRef.current) {
        return
      }

      const previous = languageRef.current
      selection.current += 1
      await applyLanguage(value)
      try {
        await saveProfileLanguage(value)
      } catch {
        await applyLanguage(previous)
      }
    },
    [applyLanguage],
  )

  const value = useMemo(() => ({ language, selectLanguage }), [language, selectLanguage])
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}
