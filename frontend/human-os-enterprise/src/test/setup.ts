import '@testing-library/react';
import i18n from '../i18n';

// Force Spanish language in test environment
await i18n.changeLanguage('es');

// Mock clipboard and window APIs if not present in jsdom
if (!navigator.clipboard) {
  Object.defineProperty(navigator, 'clipboard', {
    value: {
      writeText: async () => {},
    },
    writable: true,
  });
}
