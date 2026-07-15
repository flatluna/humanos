import { useMutation, useQuery } from '@tanstack/react-query';
import { getOfficialJobDescription, uploadJobDescriptionFile } from '../services/jobDescriptionSourceService';
import { useJobDescriptionSourceStore } from '../store/useJobDescriptionSourceStore';

export function useOfficialJobDescription() {
  return useQuery({
    queryKey: ['official-job-description'],
    queryFn: getOfficialJobDescription,
  });
}

/** Uploads a job description file to Data Lake storage. Records only
 *  the file name + storage path as employee-provided context — never
 *  pretends the file's content was analyzed (see the service's TODO
 *  for the future ingestion function).
 */
export function useUploadJobDescriptionFile() {
  const setEmployeeContext = useJobDescriptionSourceStore((state) => state.setEmployeeContext);

  return useMutation({
    mutationFn: uploadJobDescriptionFile,
    onSuccess: (result) => {
      setEmployeeContext({
        rolePurpose: '',
        mainResponsibilities: [],
        expectedResults: [],
        missingContext: null,
        uploadedFileName: result.fileName,
        uploadedFileStoragePath: result.storagePath,
      });
    },
  });
}
