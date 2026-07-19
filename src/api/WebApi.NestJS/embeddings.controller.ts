import { Body, BadRequestException, Controller, Post } from '@nestjs/common';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { IEmbeddingService } from '../../ai/Embedding.NestJS/embedding.service.interface';

@ApiTags('embeddings')
@Controller()
export class EmbeddingsController {
  constructor(private readonly embeddingService: IEmbeddingService) {}

  @Post('embeddings')
  @Post('api/embeddings')
  @ApiOperation({ summary: 'Generate an embedding for text' })
  @ApiResponse({ status: 201, description: 'Returns an embedding vector' })
  async generateEmbedding(@Body() body: { text?: string }) {
    if (!body || !body.text || !body.text.trim()) {
      throw new BadRequestException('Text is required');
    }

    return await this.embeddingService.generateEmbedding(body.text);
  }
}