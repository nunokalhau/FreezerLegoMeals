import { Injectable } from '@nestjs/common';
import Redis from 'ioredis';
import { ConversationHistory, ConversationMessage, InMemoryConversationStore } from './conversation-store';

// Constants for Redis keys and expiration
const CONVERSATION_PREFIX = 'conversation:';
const EXPIRE_SECONDS = 3600; // 1 hour expiry by default

@Injectable()
export class RedisMemoryProvider {
  private readonly redis: Redis;
  private readonly inMemoryFallback: InMemoryConversationStore;

  constructor() {
    try {
      // Try to connect to Redis at localhost:6379
      this.redis = new Redis({
        host: 'localhost',
        port: 6379,
        retryStrategy: (times) => {
          // Exponential backoff with max retries
          const delay = Math.min(times * 50, 2000);
          return delay;
        },
        // Set connection timeout for graceful fallback
        connectionTimeout: 1000,
      });

      // Listen for Redis connection events
      this.redis.on('connect', () => {
        console.log('Redis client connected');
      });
      
      this.redis.on('error', (err) => {
        console.error('Redis client error:', err);
      });

      // Set in-memory fallback when Redis is not accessible
      this.inMemoryFallback = new InMemoryConversationStore();
    } catch (error) {
      console.warn('Failed to initialize Redis, falling back to in-memory provider:', error);
      // If Redis fails, use in-memory fallback
      this.inMemoryFallback = new InMemoryConversationStore();
      this.redis = null;
    }
  }

  async getOrCreateConversation(conversationId?: string): Promise<ConversationHistory> {
    if (!conversationId?.trim()) {
      // Generate a new conversation ID from the original method
      const newConversationId = crypto.randomUUID();
      return {
        conversationId: newConversationId,
        messages: [],
      };
    }

    try {
      if (this.redis) {
        // Try to get conversation from Redis
        const conversationData = await this.redis.get(CONVERSATION_PREFIX + conversationId);
        
        if (conversationData) {
          // Parse the stored data and reset expiration
          const parsedConversation = JSON.parse(conversationData);
          await this.redis.expire(CONVERSATION_PREFIX + conversationId, EXPIRE_SECONDS);
          
          return parsedConversation;
        }
      }
      
      // Fallback to in-memory provider if Redis is not available or no data found
      console.warn('Falling back to in-memory storage for conversation:', conversationId);
      return this.inMemoryFallback.getOrCreateConversation(conversationId);
    } catch (error) {
      console.error('Error retrieving conversation from Redis, using fallback:', error);
      // If there's an error accessing Redis, fall back to in-memory storage
      return this.inMemoryFallback.getOrCreateConversation(conversationId);
    }
  }

  async appendMessages(conversationId: string, messages: ConversationMessage[]): Promise<void> {
    if (!conversationId?.trim()) {
      throw new Error('Conversation ID is required');
    }

    try {
      if (this.redis) {
        // Get existing conversation data
        const existingData = await this.redis.get(CONVERSATION_PREFIX + conversationId);
        let conversation: ConversationHistory;
        
        if (existingData) {
          conversation = JSON.parse(existingData);
        } else {
          // Create a new conversation object since it doesn't exist yet
          conversation = {
            conversationId,
            messages: [],
          };
        }
        
        // Append new messages
        conversation.messages.push(...messages);
        
        // Store back to Redis with expiration
        await this.redis.setex(
          CONVERSATION_PREFIX + conversationId,
          EXPIRE_SECONDS,
          JSON.stringify(conversation)
        );
      } else {
        // Use in-memory fallback if Redis is not available
        await this.inMemoryFallback.appendMessages(conversationId, messages);
      }
    } catch (error) {
      console.error('Error appending messages to Redis, using fallback:', error);
      // If there's an error accessing Redis, fall back to in-memory storage
      await this.inMemoryFallback.appendMessages(conversationId, messages);
    }
  }

  // Expose Redis client for testing and maintenance if needed
  getRedisClient(): Redis | null {
    return this.redis;
  }
}