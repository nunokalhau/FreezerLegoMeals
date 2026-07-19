import type { Config } from 'jest';

const config: Config = {
  projects: [
    {
      displayName: 'unit',

      rootDir: '../..',

      preset: 'ts-jest',

      testEnvironment: 'node',

      testMatch: [
        '<rootDir>/tests/unit/**/*.spec.ts',
        '<rootDir>/tests/unit/**/*.test.ts'
      ],

      moduleFileExtensions: ['ts', 'js', 'json'],

      transform: {
        '^.+\\.(t|j)s$': [
          'ts-jest',
          {
            tsconfig: '<rootDir>/api/WebApi.NestJS/tsconfig.json'
          }
        ]
      },

      collectCoverageFrom: [
        '<rootDir>/api/WebApi.NestJS/**/*.ts',
        '<rootDir>/services/Services.NestJS/**/*.ts',
        '<rootDir>/repositories/Repository.NestJS/**/*.ts',
        '!<rootDir>/**/*.module.ts',
        '!<rootDir>/**/main.ts'
      ]
    },

    {
      displayName: 'integration',

      rootDir: '../..',

      preset: 'ts-jest',

      testEnvironment: 'node',

      testMatch: [
        '<rootDir>/tests/integration/**/*.spec.ts',
        '<rootDir>/tests/integration/**/*.test.ts'
      ],

      moduleFileExtensions: ['ts', 'js', 'json'],

      transform: {
        '^.+\\.(t|j)s$': [
          'ts-jest',
          {
            tsconfig: '<rootDir>/api/WebApi.NestJS/tsconfig.json'
          }
        ]
      }
    }
  ],

  coverageDirectory: 'coverage',

  verbose: true
};

export default config;