import '@testing-library/jest-dom';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { server } from './mocks/server';

// Ant Design uses window.matchMedia for responsive breakpoints — not in jsdom
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

// Ant Design Tree / Table use ResizeObserver — not in jsdom
class ResizeObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}
global.ResizeObserver = ResizeObserverMock as unknown as typeof ResizeObserver;

// Ant Design Dropdown / Select use IntersectionObserver — not in jsdom
class IntersectionObserverMock {
  observe() {}
  unobserve() {}
  disconnect() {}
}
global.IntersectionObserver =
  IntersectionObserverMock as unknown as typeof IntersectionObserver;

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
