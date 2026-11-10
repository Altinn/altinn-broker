import { QuestionmarkCircleFillIcon } from '@navikt/aksel-icons'
import { Label, Paragraph, Popover } from '@digdir/designsystemet-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * A field's label with a help icon beside it, for use inside a `Field`. The icon sits next to the
 * label rather than in it: a button inside a label hands its clicks to the field's input.
 */
export function LabelWithHelpText({ label, helpText }: { label: ReactNode; helpText: ReactNode }) {
  return (
    <WithHelpText helpText={helpText}>
      <Label>{label}</Label>
    </WithHelpText>
  )
}

/** Anything with a help icon beside it, such as a checkbox or switch. */
export function WithHelpText({ helpText, children }: { helpText: ReactNode; children: ReactNode }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--ds-size-2)' }}>
      {children}
      <HelpTextPopover helpText={helpText} />
    </div>
  )
}

export function HelpTextPopover({ helpText }: { helpText: ReactNode }) {
  const { t } = useTranslation()
  return (
    <Popover.TriggerContext>
      <Popover.Trigger inline aria-label={t('page.helptext.open')}>
        <QuestionmarkCircleFillIcon aria-hidden style={{ color: 'var(--ds-color-base-active)', fontSize: '1.2em' }} />
      </Popover.Trigger>
      <Popover style={{ whiteSpace: 'normal' }}>
        <Paragraph>{helpText}</Paragraph>
      </Popover>
    </Popover.TriggerContext>
  )
}
