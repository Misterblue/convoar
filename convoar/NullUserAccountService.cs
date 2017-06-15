/*
 * Copyright (c) 2017 Robert Adams
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace org.herbal3d.convoar {
    // A dummy class used to fool OpenSimulator routines that there is a real user account service
    class NullUserAccountService : IUserAccountService
    {
        public UserAccount GetUserAccount(UUID scopeID, UUID userID) {
            UserAccount ua = new UserAccount(scopeID, userID, "firstname", "lastname", "firstname.lastname@example.com");
            return ua;
        }

        public UserAccount GetUserAccount(UUID scopeID, string FirstName, string LastName) {
            UserAccount ua = new UserAccount(scopeID, FirstName, LastName, "firstname.lastname@example.com");
            return ua;
        }

        public UserAccount GetUserAccount(UUID scopeID, string Email) {
            UserAccount ua = new UserAccount(scopeID, "firstname", "lastname", Email);
            return ua;
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, string query) {
            UserAccount ua = new UserAccount(scopeID, "firstname", "lastname", "firstname.lastname@example.com");
            return new List<UserAccount>() { ua } ;
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, List<string> IDs) {
            UserAccount ua = new UserAccount(scopeID, "firstname", "lastname", "firstname.lastname@example.com");
            return new List<UserAccount>() { ua } ;
            throw new NotImplementedException();
        }

        public List<UserAccount> GetUserAccountsWhere(UUID scopeID, string where) {
            throw new NotImplementedException();
        }

        public void InvalidateCache(UUID userID) {
            throw new NotImplementedException();
        }

        public bool StoreUserAccount(UserAccount data) {
            throw new NotImplementedException();
        }
    }
}
