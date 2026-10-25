import type { Point, RenderTargetInitFunction } from 'hanzi-writer';

type BoundEvent = { getPoint(): Point; preventDefault(): void };
const svgNamespace = 'http://www.w3.org/2000/svg';

/** SVG render target with local Pointer Events and disposable listeners for mouse, pen and touch. */
class PointerTarget {
  readonly node: SVGSVGElement | SVGGElement;
  private pointerId?: number;
  constructor(
    readonly svg: SVGSVGElement | SVGGElement,
    readonly defs: SVGDefsElement,
    private abort: AbortController,
  ) {
    this.node = svg;
  }
  createSubRenderTarget(): PointerTarget {
    const group = document.createElementNS(svgNamespace, 'g');
    this.svg.appendChild(group);
    return new PointerTarget(group, this.defs, this.abort);
  }
  addPointerStartListener(callback: (event: BoundEvent) => void) {
    this.svg.addEventListener(
      'pointerdown',
      (event) => {
        if (!(event instanceof PointerEvent)) return;
        if (!event.isPrimary || event.button !== 0 || this.pointerId !== undefined) return;
        this.pointerId = event.pointerId;
        this.svg.setPointerCapture(event.pointerId);
        callback(this._eventify(event, this._getMousePoint));
      },
      { signal: this.abort.signal },
    );
  }
  addPointerMoveListener(callback: (event: BoundEvent) => void) {
    this.svg.addEventListener(
      'pointermove',
      (event) => {
        if (!(event instanceof PointerEvent)) return;
        if (event.pointerId === this.pointerId)
          callback(this._eventify(event, this._getMousePoint));
      },
      { signal: this.abort.signal },
    );
  }
  addPointerEndListener(callback: () => void) {
    this.svg.addEventListener(
      'pointerup',
      (event) => {
        if (!(event instanceof PointerEvent)) return;
        if (event.pointerId !== this.pointerId) return;
        this.pointerId = undefined;
        callback();
      },
      { signal: this.abort.signal },
    );
  }
  getBoundingClientRect() {
    return this.svg.getBoundingClientRect();
  }
  updateDimensions(width: string | number, height: string | number) {
    this.svg.setAttribute('width', String(width));
    this.svg.setAttribute('height', String(height));
  }
  _eventify<T extends Event>(event: T, point: (event: T) => Point): BoundEvent {
    return {
      getPoint: () => point.call(this, event),
      preventDefault: () => event.preventDefault(),
    };
  }
  _getMousePoint(event: MouseEvent): Point {
    const bounds = this.getBoundingClientRect();
    return { x: event.clientX - bounds.left, y: event.clientY - bounds.top };
  }
  _getTouchPoint(event: TouchEvent): Point {
    const bounds = this.getBoundingClientRect();
    return { x: event.touches[0].clientX - bounds.left, y: event.touches[0].clientY - bounds.top };
  }
}

export function pointerTarget(abort: AbortController): RenderTargetInitFunction<SVGElement> {
  return (element, width = '100%', height = '100%') => {
    const host = typeof element === 'string' ? document.getElementById(element) : element;
    if (!host) throw new Error('Writing surface is unavailable.');
    const svg = document.createElementNS(svgNamespace, 'svg');
    const defs = document.createElementNS(svgNamespace, 'defs');
    svg.appendChild(defs);
    host.appendChild(svg);
    const target = new PointerTarget(svg, defs, abort);
    target.updateDimensions(width ?? '100%', height ?? '100%');
    return target;
  };
}
