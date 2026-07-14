import { useContext } from 'react';
import { ThemeContext } from '../contexts/ThemeContext';

export function useThemeMode() {
  return useContext(ThemeContext);
}
