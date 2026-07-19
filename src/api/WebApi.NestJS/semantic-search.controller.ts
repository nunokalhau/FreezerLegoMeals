import { Body, BadRequestException, Controller, Post } from '@nestjs/common';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { SemanticSearchService } from '../../ai/SemanticSearch/NestJS/semantic-search.service';

@ApiTags('semantic-search')
@Controller('api/semantic-search')
export class SemanticSearchController {
  constructor(private readonly semanticSearchService: SemanticSearchService) {}

  @Post()
  @ApiOperation({ summary: 'Search recipes semantically' })
  @ApiResponse({ status: 201, description: 'Returns ranked semantic recipe matches' })
  async search(@Body() body: { query?: string; topK?: number }) {
    if (!body || !body.query || !body.query.trim()) {
      throw new BadRequestException('Query is required');
    }

    return await this.semanticSearchService.search(body.query, body.topK ?? 5);
  }
}