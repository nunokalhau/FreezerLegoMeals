// Configuration file for Freezer Lego Meals frontend
export const config = {
  // Base URL for API endpoints
  // This can be changed to point to any of the three implementations:
  // .NET: http://localhost:5001
  // Python: http://localhost:5000  
  // NestJS: http://localhost:3000
  apiUrl: 'http://localhost:3000',
  
  // API timeout in milliseconds
  apiTimeout: 10000,
  
  // Future authentication support can be added here
  // auth: {
  //   token: null,
  //   refreshToken: null,
  // }
};