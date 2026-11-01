import '@testing-library/jest-dom/vitest'

// jsdom has no layout: give observed elements a fixed size so size-dependent components (the treemap) draw.
class FixedResizeObserver {
  private readonly callback: ResizeObserverCallback

  constructor(callback: ResizeObserverCallback) {
    this.callback = callback
  }

  observe(target: Element) {
    this.callback([{ target, contentRect: { width: 900, height: 420 } } as ResizeObserverEntry], this)
  }

  unobserve() {}

  disconnect() {}
}
globalThis.ResizeObserver = FixedResizeObserver as unknown as typeof ResizeObserver

// jsdom does not implement the modal <dialog> API.
HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
  this.setAttribute('open', '')
}
HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
  this.removeAttribute('open')
}
