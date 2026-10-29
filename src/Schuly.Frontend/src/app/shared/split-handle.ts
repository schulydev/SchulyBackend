import { Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';

const KEY_STEP = 16;
const KEY_STEP_LARGE = 64;

/**
 * Draggable, keyboard-operable divider (WAI-ARIA window splitter). It sits right after the pane it resizes and
 * reports that pane's new width in pixels; the parent owns and persists the width.
 */
@Component({
  selector: 'app-split-handle',
  host: {
    role: 'separator',
    tabindex: '0',
    'aria-orientation': 'vertical',
    '[attr.aria-label]': 'label()',
    '[attr.aria-valuemin]': 'min()',
    '[attr.aria-valuemax]': 'max()',
    '[attr.aria-valuenow]': 'width()',
    class:
      'group relative -mx-px hidden w-2 shrink-0 cursor-col-resize touch-none items-stretch justify-center outline-none lg:flex focus-visible:z-10',
    '(pointerdown)': 'start($event)',
    '(pointermove)': 'drag($event)',
    '(pointerup)': 'end($event)',
    '(pointercancel)': 'end($event)',
    '(keydown)': 'key($event)',
    '(dblclick)': 'widthChange.emit(initial())',
  },
  template: `<span
    class="bg-border group-hover:bg-ring group-focus-visible:bg-ring group-focus-visible:w-0.5 w-px transition-colors"
    [class.bg-ring]="dragging()"
    aria-hidden="true"
  ></span>`,
})
export class SplitHandle {
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly label = input.required<string>();
  /** Width of the pane before the handle, in px. */
  readonly width = input.required<number>();
  readonly min = input(200);
  readonly max = input(600);
  readonly initial = input(320);
  readonly widthChange = output<number>();

  protected readonly dragging = signal(false);
  private origin = 0;
  private startWidth = 0;
  private readonly rtl = computed(() => document.dir === 'rtl');

  private clamp(value: number): number {
    return Math.min(this.max(), Math.max(this.min(), value));
  }

  protected start(event: PointerEvent): void {
    this.dragging.set(true);
    this.origin = event.clientX;
    this.startWidth = this.width();
    this.element.nativeElement.setPointerCapture(event.pointerId);
    event.preventDefault();
  }

  protected drag(event: PointerEvent): void {
    if (!this.dragging()) return;
    const delta = (event.clientX - this.origin) * (this.rtl() ? -1 : 1);
    this.widthChange.emit(this.clamp(this.startWidth + delta));
  }

  protected end(event: PointerEvent): void {
    if (!this.dragging()) return;
    this.dragging.set(false);
    this.element.nativeElement.releasePointerCapture(event.pointerId);
  }

  protected key(event: KeyboardEvent): void {
    const step = event.shiftKey ? KEY_STEP_LARGE : KEY_STEP;
    const sign = this.rtl() ? -1 : 1;
    let next: number | null = null;
    if (event.key === 'ArrowLeft') next = this.width() - step * sign;
    else if (event.key === 'ArrowRight') next = this.width() + step * sign;
    else if (event.key === 'Home') next = this.min();
    else if (event.key === 'End') next = this.max();
    else if (event.key === 'Enter') next = this.initial();
    if (next === null) return;
    event.preventDefault();
    this.widthChange.emit(this.clamp(next));
  }
}
