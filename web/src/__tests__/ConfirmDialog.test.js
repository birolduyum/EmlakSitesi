import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import ConfirmDialog from '../components/admin/ConfirmDialog.vue'

function host() {
  return mount(
    {
      components: { ConfirmDialog },
      data: () => ({ open: false, log: [] }),
      template: `<div><button id="opener" @click="open = true">Aç</button>
        <ConfirmDialog :open="open" title="Silinsin mi?" message="Deniz manzaralı ilanı silinecek." confirm-label="Sil" danger
          @confirm="log.push('confirm'); open = false" @cancel="log.push('cancel'); open = false" /></div>`
    },
    { attachTo: document.body }
  )
}

describe('ConfirmDialog', () => {
  it('renders nothing while closed and a labelled modal dialog when open', async () => {
    const w = host()
    expect(w.find('[role="dialog"]').exists()).toBe(false)
    await w.find('#opener').trigger('click')
    const d = w.find('[role="dialog"]')
    expect(d.attributes('aria-modal')).toBe('true')
    expect(d.text()).toContain('Deniz manzaralı ilanı silinecek.')
    expect(d.attributes('aria-labelledby')).toBeTruthy()
    w.unmount()
  })
  it('Escape cancels', async () => {
    const w = host()
    await w.find('#opener').trigger('click')
    await w.find('[role="dialog"]').trigger('keydown', { key: 'Escape' })
    expect(w.vm.log).toEqual(['cancel'])
    w.unmount()
  })
  it('confirm button emits confirm and is danger-styled', async () => {
    const w = host()
    await w.find('#opener').trigger('click')
    const btn = w.find('[data-testid="confirm"]')
    expect(btn.text()).toBe('Sil')
    expect(btn.classes()).toContain('danger')
    await btn.trigger('click')
    expect(w.vm.log).toEqual(['confirm'])
    w.unmount()
  })
  it('focus_is_trapped_and_returned', async () => {
    const w = host()
    const opener = w.find('#opener').element
    opener.focus()
    await w.find('#opener').trigger('click')
    await nextTick()
    const dialog = w.find('[role="dialog"]').element
    expect(dialog.contains(document.activeElement)).toBe(true)
    const buttons = [...dialog.querySelectorAll('button')]
    buttons.at(-1).focus()
    await w.find('[role="dialog"]').trigger('keydown', { key: 'Tab' })
    expect(document.activeElement).toBe(buttons[0])
    await w.find('[role="dialog"]').trigger('keydown', { key: 'Tab', shiftKey: true })
    expect(document.activeElement).toBe(buttons.at(-1))
    await w.find('[data-testid="cancel"]').trigger('click')
    await nextTick()
    await nextTick()
    expect(document.activeElement).toBe(opener)
    w.unmount()
  })
  it('clicking the backdrop cancels', async () => {
    const w = host()
    await w.find('#opener').trigger('click')
    await w.find('.backdrop').trigger('click')
    expect(w.vm.log).toEqual(['cancel'])
    w.unmount()
  })
})
