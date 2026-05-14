import { render, act } from '@testing-library/react';
import { ThemeProvider, useTheme } from '../shared/ThemeContext';

function ThemeDisplay() {
  const { theme, toggle } = useTheme();
  return (
    <div>
      <span data-testid="theme">{theme}</span>
      <button onClick={toggle}>toggle</button>
    </div>
  );
}

function setup() {
  return render(
    <ThemeProvider>
      <ThemeDisplay />
    </ThemeProvider>,
  );
}

beforeEach(() => {
  localStorage.clear();
  document.documentElement.removeAttribute('data-theme');
});

describe('ThemeProvider', () => {
  it('defaults to light theme', () => {
    const { getByTestId } = setup();
    expect(getByTestId('theme').textContent).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('restores dark theme from localStorage', () => {
    localStorage.setItem('granas:theme', 'dark');
    const { getByTestId } = setup();
    expect(getByTestId('theme').textContent).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('toggle switches theme and persists to localStorage', () => {
    const { getByTestId, getByRole } = setup();
    expect(getByTestId('theme').textContent).toBe('light');

    act(() => { getByRole('button').click(); });

    expect(getByTestId('theme').textContent).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('granas:theme')).toBe('dark');
  });

  it('toggle back to light updates attribute and localStorage', () => {
    localStorage.setItem('granas:theme', 'dark');
    const { getByTestId, getByRole } = setup();

    act(() => { getByRole('button').click(); });

    expect(getByTestId('theme').textContent).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    expect(localStorage.getItem('granas:theme')).toBe('light');
  });
});
