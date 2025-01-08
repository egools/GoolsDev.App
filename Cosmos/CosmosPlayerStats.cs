using GoolsDev.App.Controllers;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GoolsDev.App.Cosmos
{
    public class CosmosPlayerStats : ICosmosPlayerStats
    {
        private readonly Container _statsContainer;

        public CosmosPlayerStats(CosmosClient cosmosClient)
        {
            _statsContainer = cosmosClient.GetContainer("cosmos-bmgc-goolsdev", "nfl-player-stats");
        }

        public async Task<List<NflPlayerStatsDocument>> GetPlayerStats(int year, IEnumerable<string> playerIds)
        {
            var query = new QueryDefinition(
               "select * from players p " +
               "where p.year = @Year " +
               "and array_contains_any(@PlayerIds, p.playerId)")
               .WithParameter("@Year", year)
               .WithParameter("@PlayerIds", playerIds);
            var requestOptions = new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(year),
                MaxItemCount = 16
            };

            using var resultSet = _statsContainer.GetItemQueryIterator<NflPlayerStatsDocument>(query, requestOptions: requestOptions);
            var players = new List<NflPlayerStatsDocument>();
            while (resultSet.HasMoreResults)
            {
                FeedResponse<NflPlayerStatsDocument> response = await resultSet.ReadNextAsync();
                players.AddRange(response.ToList());
            }

            return players;
        }

        public async Task<List<NflPlayerStatsDocument>> GetAllPlayers(int year)
        {
            var query = new QueryDefinition("select * from players p where p.year = @Year")
                .WithParameter("@Year", year);
            var requestOptions = new QueryRequestOptions()
            {
                PartitionKey = new PartitionKey(year),
                MaxItemCount = 16
            };

            using var resultSet = _statsContainer.GetItemQueryIterator<NflPlayerStatsDocument>(query, requestOptions: requestOptions);
            var players = new List<NflPlayerStatsDocument>();
            while (resultSet.HasMoreResults)
            {
                FeedResponse<NflPlayerStatsDocument> response = await resultSet.ReadNextAsync();
                players.AddRange(response.ToList());
            }
            return players;
        }

    }

    public interface ICosmosPlayerStats
    {
        Task<List<NflPlayerStatsDocument>> GetAllPlayers(int year);
        Task<List<NflPlayerStatsDocument>> GetPlayerStats(int year, IEnumerable<string> playerIds);
    }
}
