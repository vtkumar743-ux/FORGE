import { createContext, useContext } from 'react'

/**
 * The branch a page is scoped to, if any. Branch pages render the same section types as
 * the network-wide pages — the locator, timetable and pricing all narrow themselves from
 * here rather than each section growing a branch prop that only some callers pass.
 */
export const BranchScopeContext = createContext<string | undefined>(undefined)

export function useBranchScope(): string | undefined {
  return useContext(BranchScopeContext)
}
