import { useState, useMemo } from 'react';

const levenshteinDistance = (str1, str2) => {
  const m = str1.length;
  const n = str2.length;
  const dp = Array.from({ length: m + 1 }, () => Array(n + 1).fill(0));

  for (let i = 0; i <= m; i++) dp[i][0] = i;
  for (let j = 0; j <= n; j++) dp[0][j] = j;

  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      if (str1[i - 1] === str2[j - 1]) {
        dp[i][j] = dp[i - 1][j - 1];
      } else {
        dp[i][j] = Math.min(
          dp[i - 1][j - 1],
          dp[i - 1][j],
          dp[i][j - 1]
        ) + 1;
      }
    }
  }

  return dp[m][n];
};

const normalizeText = (text) => {
  return text
    ?.toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9\s]/g, '') || '';
};

// Whether an item's name, description or a tag contains every word typed (blank matches all)
export const matchesSearch = (item, searchTerm) =>
  searchTerm
    .toLowerCase()
    .split(" ")
    .map((term) => term.trim())
    .filter((term) => term.length > 0)
    .every(
      (term) =>
        item.name.toLowerCase().includes(term) ||
        (item.description && item.description.toLowerCase().includes(term)) ||
        (item.tags && item.tags.some((tag) => tag.name.toLowerCase().includes(term))),
    );

export const useMenuSearch = (items) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [maxDistance, setMaxDistance] = useState(3);

  const searchResults = useMemo(() => {
    if (!searchTerm.trim()) return items;

    const normalizedSearch = normalizeText(searchTerm);
    const searchWords = normalizedSearch.split(/\s+/).filter(Boolean);

    return items.map(item => {
      let nameScore = 0;
      let descScore = 0;
      let tagScore = 0;

      const normalizedName = normalizeText(item.name);
      const normalizedDesc = normalizeText(item.description);
      const normalizedTags = item.tags?.map(tag => normalizeText(tag.name)) || [];

      searchWords.forEach(searchWord => {

        if (normalizedName.includes(searchWord)) {
          nameScore = 1;
        }
        if (normalizedDesc.includes(searchWord)) {
          descScore = 0.8;
        }
      
        const hasTagMatch = item.tags?.some(tag => 
          normalizeText(tag.name).includes(searchWord)
        );
        if (hasTagMatch) {
          tagScore = 1;
        }

        if (!nameScore && !descScore && !tagScore) {
          const nameWords = normalizedName.split(/\s+/);
          const minNameDistance = Math.min(...nameWords.map(word => 
            levenshteinDistance(searchWord, word)
          ));
          if (minNameDistance <= 2) {
            nameScore = Math.max(nameScore, 1 - (minNameDistance * 0.3));
          }

          const descWords = normalizedDesc.split(/\s+/);
          const minDescDistance = Math.min(...descWords.map(word => 
            levenshteinDistance(searchWord, word)
          ));
          if (minDescDistance <= 2) {
            descScore = Math.max(descScore, 0.8 - (minDescDistance * 0.3));
          }

          const minTagDistance = Math.min(...normalizedTags.map(tag => 
            levenshteinDistance(searchWord, tag)
          ), Infinity);
          if (minTagDistance <= 2) {
            tagScore = Math.max(tagScore, 1 - (minTagDistance * 0.3));
          }
        }
      });

      const finalScore = Math.max(nameScore, descScore, tagScore);

      return {
        ...item,
        searchScore: finalScore
      };
    })
    .filter(item => item.searchScore > 0.4)
    .sort((a, b) => b.searchScore - a.searchScore);
  }, [items, searchTerm, maxDistance]);

  return {
    searchTerm,
    setSearchTerm,
    searchResults,
    setMaxDistance
  };
};

export default useMenuSearch;