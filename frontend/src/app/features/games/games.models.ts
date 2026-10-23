export interface GameSummary {
  id: number;
  name: string;
  released: string | null;
  backgroundImage: string | null;
  rating: number;
  metacritic: number | null;
  genres: string[];
  platforms: string[];
}

export interface GameDetails extends GameSummary {
  description: string | null;
  website: string | null;
  developers: string[];
  publishers: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
