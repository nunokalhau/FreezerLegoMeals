import { Injectable } from '@nestjs/common';

@Injectable()
export abstract class BaseRepository {
  protected constructor() {}

  // Common methods that can be shared across repositories
  protected validateEntity<T>(entity: T): boolean {
    return entity !== null && entity !== undefined;
  }

  protected handleError(error: any, operation: string): void {
    console.error(`Error in ${operation}:`, error);
    throw new Error(`Failed to ${operation}`);
  }
}