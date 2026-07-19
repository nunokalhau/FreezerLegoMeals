// src/pages/Assistant.tsx

import React, { useState, useRef, useEffect } from 'react';
import { apiService } from '../services/apiService';
import type { AssistantChatRequest, AssistantChatResponse } from '../services/apiService';
import './assistant.css';

interface Message {
  id: string;
  content: string;
  role: 'user' | 'assistant';
  timestamp: Date;
}

const Assistant: React.FC = () => {
  const [inputMessage, setInputMessage] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom of chat
  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!inputMessage.trim() || isLoading) return;

    // Add user message to UI immediately
    const userMessage: Message = {
      id: Date.now().toString(),
      content: inputMessage,
      role: 'user',
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInputMessage('');
    setIsLoading(true);
    setError(null);

    try {
      // Send to backend
      const request: AssistantChatRequest = {
        message: inputMessage,
        conversationId: messages.length > 0 ? messages[0].id : undefined
      };

      const response: AssistantChatResponse = await apiService.chatWithAssistant(request);
      
      // Add assistant response to UI
      const assistantMessage: Message = {
        id: response.conversationId,
        content: response.response,
        role: 'assistant',
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, assistantMessage]);
    } catch (err) {
      setError('Failed to get response from assistant');
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  };

  const clearChat = () => {
    setMessages([]);
    setError(null);
  };

  return (
    <div className="assistant">
      <h1>AI Assistant</h1>
      
      <div className="chat-container">
        <div className="chat-header">
          <h2>Meal Planning Help</h2>
          <button onClick={clearChat} className="clear-button">Clear Chat</button>
        </div>
        
        <div className="messages-container">
          {messages.length === 0 ? (
            <div className="welcome-message">
              <p>Welcome to the Freezer Lego Meals AI Assistant!</p>
              <p>Ask me about recipes, meal planning, or get suggestions for ingredients.</p>
            </div>
          ) : (
            messages.map((message) => (
              <div 
                key={message.id} 
                className={`message ${message.role}`}
              >
                <div className="message-content">
                  {message.content}
                </div>
                <div className="message-timestamp">
                  {message.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </div>
              </div>
            ))
          )}
          {isLoading && (
            <div className="message assistant loading">
              <div className="message-content">
                <div className="spinner"></div>
                Thinking...
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>
        
        <form onSubmit={handleSubmit} className="input-container">
          <input
            type="text"
            value={inputMessage}
            onChange={(e) => setInputMessage(e.target.value)}
            placeholder="Ask about recipes, ingredients, or meal planning..."
            disabled={isLoading}
            className="message-input"
          />
          <button 
            type="submit" 
            disabled={isLoading || !inputMessage.trim()}
            className="send-button"
          >
            Send
          </button>
        </form>
      </div>
      
      {error && <div className="error">{error}</div>}
    </div>
  );
};

export default Assistant;