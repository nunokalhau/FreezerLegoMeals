import { Injectable } from '@nestjs/common';
import Redis from 'ioredis';
import { ConversationHistory, ConversationMessage } from '../../services/Services.NestJS/conversation-store';
import { IMemoryProvider } from './memory-provider.interface';

// Constants for Redis keys and expiration
const CONVERSATION_PREFIX = 'conversation:';
const EXPIRE_SECONDS = 3600; // 1 hour expiry by default
const MAX_CONVERSATIONS = 1000; // Prevent unlimited conversation growth
const MAX_MESSAGES_PER_CONVERSATION = 100; // Prevent unlimited messages per conversation

/**
 * Redis-backed implementation of the memory provider interface.
 * Compatible with the .NET conversation memory provider pattern.
 */
@Injectable()
export class RedisMemoryProvider implements IMemoryProvider {
  private readonly redis: Redis;
  private readonly logger = console;

  constructor() {
    try {
      // Initialize Redis connection with enhanced security and performance parameters
      this.redis = new Redis({
        host: process.env.REDIS_HOST || 'localhost',
        port: Number(process.env.REDIS_PORT) || 6379,
        password: process.env.REDIS_PASSWORD,
        db: Number(process.env.REDIS_DB) || 0,
        retryStrategy: (times) => {
          // Exponential backoff with max retries
          const delay = Math.min(times * 50, 2000);
          return delay;
        },
        connectionTimeout: 1000, // Set connection timeout to prevent hanging
        // Add more security and performance parameters
        maxRetriesPerRequest: 3,
        enableOfflineQueue: true,
      });

      // Listen for Redis connection events
      this.redis.on('connect', () => {
        this.logger.log('Redis client connected');
      });
      
      this.redis.on('error', (err) => {
        this.logger.error('Redis client error:', err);
      });

      this.redis.on('reconnecting', () => {
        this.logger.warn('Redis client reconnecting...');
      });
    } catch (error) {
      this.logger.warn('Failed to initialize Redis, falling back to in-memory provider:', error);
      // If Redis fails, we don't initialize a Redis client but will use in-memory fallback
      this.redis = null;
    }
  }

  async getOrCreateConversation(conversationId?: string): Promise<ConversationHistory> {
    if (!conversationId?.trim()) {
      // Generate a new conversation ID 
      const newConversationId = crypto.randomUUID();
      return {
        conversationId: newConversationId,
        messages: [],
      };
    }

    try {
      if (this.redis) {
        // Try to get existing conversation from Redis
        const existingConversation = await this.redis.get(`${CONVERSATION_PREFIX}${conversationId}`);
        
        if (existingConversation) {
          // Reset expiration on access
          await this.redis.expire(`${CONVERSATION_PREFIX}${conversationId}`, EXPIRE_SECONDS);
          return JSON.parse(existingConversation);
        }
      }
      
      // If Redis is not available or no conversation found, we fall back to using InMemoryConversationStore
      const inMemoryProvider = new (await import('../../services/Services.NestJS/conversation-store')).InMemoryConversationStore();
      return inMemoryProvider.getOrCreateConversation(conversationId);
    } catch (error) {
      this.logger.error('Error retrieving conversation from Redis, using fallback:', error);
      // If there's an error accessing Redis, fall back to in-memory storage
      const inMemoryProvider = new (await import('../../services/Services.NestJS/conversation-store')).InMemoryConversationStore();
      return inMemoryProvider.getOrCreateConversation(conversationId);
    }
  }

  async appendMessages(conversationId: string, messages: ConversationMessage[]): Promise<void> {
    if (!conversationId?.trim()) {
      throw new Error('Conversation ID is required');
    }

    try {
      if (this.redis) {
        // Get existing conversation
        const existingConversation = await this.redis.get(`${CONVERSATION_PREFIX}${conversationId}`);
        
        let conversation: ConversationHistory;
        
        if (existingConversation) {
          conversation = JSON.parse(existingConversation);
        } else {
          // Create a new conversation object since it doesn't exist yet
          conversation = {
            conversationId,
            messages: [],
          };
        }
        
        // Apply resource limits to prevent unbounded growth
        this.applyResourceLimits(conversation, messages);
        
        // Add the new messages
        conversation.messages.push(...messages);
        
        // Store back to Redis with expiration
        await this.redis.setex(
          `${CONVERSATION_PREFIX}${conversationId}`,
          EXPIRE_SECONDS,
          JSON.stringify(conversation)
        );
      } else {
        // Use in-memory fallback if Redis is not available
        const inMemoryProvider = new (await import('../../services/Services.NestJS/conversation-store')).InMemoryConversationStore();
        await inMemoryProvider.appendMessages(conversationId, messages);
      }
    } catch (error) {
      this.logger.error('Error appending messages to Redis, using fallback:', error);
      // If there's an error accessing Redis, fall back to in-memory storage
      const inMemoryProvider = new (await import('../../services/Services.NestJS/conversation-store')).InMemoryConversationStore();
      await inMemoryProvider.appendMessages(conversationId, messages);
    }
  }

  /**
   * Apply resource limits to prevent unbounded conversation growth
   */
  private applyResourceLimits(conversation: ConversationHistory, newMessages: ConversationMessage[]): void {
    // Truncate messages if we're close to the limit
    const totalMessages = conversation.messages.length + newMessages.length;
    
    if (totalMessages > MAX_MESSAGES_PER_CONVERSATION) {
      // Keep only the most recent messages
      const excessCount = totalMessages - MAX_MESSAGES_PER_CONVERSATION;
      conversation.messages.splice(0, excessCount);
    }
    
    // If conversation is too large, truncate it to prevent memory issues
    if (conversation.messages.length > MAX_MESSAGES_PER_CONVERSATION) {
      conversation.messages = conversation.messages.slice(-MAX_MESSAGES_PER_CONVERSATION);
    }
  }
}