/** Mirrors the JSON returned by GET /api/about. */
export interface AboutInfoDto {
  readonly applicationName: string;
  readonly description: string;
}