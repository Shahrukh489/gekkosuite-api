Create User or Assign User Role was kept at org level only for the reasons
1. If a store admin can create a user, he is part of your organization, that is something you should approve first
2. Store admin can send a ticket request to add a user, this is how all major companies work, you can then get a notification in your organization and approve or deny
3. No room for a store admin mistakenly creating another store admin
4. Store admin still have the ability to disable a user in the store if needed
5. Store admin will be able to remove a user's role
6. Store admin can not delete a user, org admin should approve removal of a user and follow procedure
7. Store admin can not assign a user a role, only Org admin when he creates the user gives him the role, prevents store admin from making someone an elevated user in the store which you did not want. 


Products  in seperate database tables
1. No one store can mess up information for another store
2. Each store manages its own copy of data
3. This brings the challenge of another local store wanting to see if a store nearby has a product, for this org admin can give a user in the store a read-only products permission role to the other store, controlling who sees what across different stores


Customers in a store seperate database table
1. Customers are local to there stores so they have accounts there
2. No one store can mess up a customers account for a different store
3. Stores running promotions can give there customer account different discounts
4. This brings the issue of doing customer loyalty, where he gets points based on purchases across all stores in the organization, for this we will add a loyalty feature where the organization can see customers with same email across different stores and grant rewards per store. 

