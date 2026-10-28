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

export interface FilterOption {
  value: string;
  name: string;
}

export interface GameFilterOptions {
  genres: FilterOption[];
  platforms: FilterOption[];
  orderings: FilterOption[];
}

export interface CompanyOption {
  slug: string;
  name: string;
  gamesCount: number;
}

export type CompanyKind = 'developer' | 'publisher';
