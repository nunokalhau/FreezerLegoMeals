import { Module } from '@nestjs/common';
import { RedisMemoryProvider } from './redis-memory-provider';
import { IMemoryProvider } from './memory-provider.interface';

@Module({
  providers: [
    {
      provide: IMemoryProvider,
      useClass: RedisMemoryProvider,
    },
  ],
  exports: [IMemoryProvider],
})
export class MemoryProviderModule {}