export interface Game {
	id: number;
	name: string;
	background_image: string | null;
	released: string | null;
	rating: number;
	metacritic: number | null;
	genres: string[];
	platforms: string[];
}
