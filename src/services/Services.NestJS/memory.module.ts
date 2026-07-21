import { Module } from '@nestjs/common';
import { RedisMemoryProvider } from './redis-memory-provider';
import { InMemoryConversationStore } from './conversation-store';

// Use the existing InMemoryConversationStore as a fallback for IMemoryProvider
// This allows us to preserve existing functionality while enabling Redis provider selection
export const MEMORY_PROVIDER_TOKEN = 'MEMORY_PROVIDER';

@Module({
  providers: [
    {
      provide: MEMORY_PROVIDER_TOKEN,
      useClass: RedisMemoryProvider,
    },
    InMemoryConversationStore,
  ],
  exports: [MEMORY_PROVIDER_TOKEN, InMemoryConversationStore],
})
export class MemoryModule {}