import { isAppLanguage, type AppLanguage } from '../i18n/config'
import { apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'

type ProfileLanguageResponse = {
  language: string
}

export async function fetchProfileLanguage(): Promise<AppLanguage> {
  const body = await apiFetch<ProfileLanguageResponse>(`${BROKER_API_PREFIX}/profile/language`)
  return isAppLanguage(body.language) ? body.language : 'nb'
}

export async function saveProfileLanguage(language: AppLanguage): Promise<void> {
  await apiFetch(`${BROKER_API_PREFIX}/profile/language`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ language }),
  })
}
