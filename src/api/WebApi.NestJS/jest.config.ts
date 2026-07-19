import type { Config } from 'jest';

const config: Config = {
  projects: [
    {
      displayName: 'unit',

      rootDir: '.',

      preset: 'ts-jest',

      testEnvironment: 'node',

      testMatch: [
        '<rootDir>/tests/unit/**/*.spec.ts',
        '<rootDir>/tests/unit/**/*.test.ts'
      ],

      moduleFileExtensions: ['ts', 'js', 'json'],

      transform: {
        '^.+\\.(t|j)s$': 'ts-jest'
      },

      collectCoverageFrom: [
        'src/**/*.ts',
        '!src/**/*.module.ts',
        '!src/**/main.ts'
      ]
    },

    {
      displayName: 'integration',

      rootDir: '.',

      preset: 'ts-jest',

      testEnvironment: 'node',

      testMatch: [
        '<rootDir>/tests/integration/**/*.spec.ts',
        '<rootDir>/tests/integration/**/*.test.ts'
      ],

      moduleFileExtensions: ['ts', 'js', 'json'],

      transform: {
        '^.+\\.(t|j)s$': 'ts-jest'
      }
    }
  ],

  coverageDirectory: 'coverage',

  verbose: true
};

export default config;