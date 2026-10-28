export type SectionStatus = 'ok' | 'private' | 'nodata';

/** Steam hides data per the user's privacy settings, so every section says whether it was available. */
export interface SteamSection<T> {
  status: SectionStatus;
  data: T | null;
}

export interface SteamProfile {
  steamId: string;
  personaName: string;
  profileUrl: string;
  avatarUrl: string;
  status: string;
  currentGame: string | null;
  countryCode: string | null;
  memberSince: string | null;
  isPublic: boolean;
}

export interface SteamGame {
  appId: number;
  name: string;
  playtimeMinutes: number;
  playtimeLastTwoWeeksMinutes: number;
  lastPlayed: string | null;
  iconUrl: string | null;
  headerUrl: string;
}

export interface SteamGameLibrary {
  count: number;
  totalPlaytimeMinutes: number;
  games: SteamGame[];
}

export interface SteamFriend {
  steamId: string;
  personaName: string;
  avatarUrl: string;
  profileUrl: string;
  status: string;
  friendSince: string | null;
}

export interface SteamAchievement {
  apiName: string;
  name: string;
  description: string | null;
  achieved: boolean;
  unlockedAt: string | null;
}

export interface SteamAchievements {
  gameName: string;
  unlocked: number;
  total: number;
  achievements: SteamAchievement[];
}
