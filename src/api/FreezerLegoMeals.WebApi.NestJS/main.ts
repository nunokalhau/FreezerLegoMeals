import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { SwaggerModule, DocumentBuilder } from '@nestjs/swagger';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  
  const config = new DocumentBuilder()
    .setTitle('Freezer Lego Meals API')
    .setDescription('The Freezer Lego Meals API description')
    .setVersion('1.0')
    .addTag('meals')
    .build();
    
  const document = SwaggerModule.createDocument(app, config);
  SwaggerModule.setup('api/docs', app, document);
  
  app.setGlobalPrefix('api');
  await app.listen(3000);
}
bootstrap();