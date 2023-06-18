using System.IdentityModel.Tokens.Jwt;

namespace StorageMicroService.Models.Services.Infrastructure
{
    public static class JwtDecoder
    {
        public static int GetIdFromToken(string token)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwtToken = tokenHandler.ReadJwtToken(token);

            if (jwtToken.Payload.TryGetValue("id", out object idValue) && Convert.ToInt32(idValue) > 0)
            {
                return Convert.ToInt32(idValue);
            }
            else
            {
                return -1;
            }
        }
    }
}
